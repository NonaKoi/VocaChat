using System.Text;
using System.Text.Json;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 使用 OpenAI 兼容模型为一次聊天生成语义计划，并在结果无效时退回规则导演。
/// </summary>
public sealed class OpenAiCompatibleConversationDirector : IConversationDirector
{
    private const int DirectorTopicMaximumLength = 80;
    private const int DirectorGoalMaximumLength = 200;
    private const int DirectorListItemMaximumLength = 120;
    private const int DirectorListMaximumCount = 5;
    private readonly OpenAiCompatibleChatClient _chatClient;
    private readonly AiMessageGenerationOptions _options;
    private readonly AiConversationContextBuilder _contextBuilder;
    private readonly ConversationActionPlanner _actionPlanner;
    private readonly RuleBasedConversationDirector _fallbackDirector;

    public OpenAiCompatibleConversationDirector(
        OpenAiCompatibleChatClient chatClient,
        AiMessageGenerationOptions options,
        AiConversationContextBuilder contextBuilder,
        ConversationActionPlanner actionPlanner)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _contextBuilder = contextBuilder
            ?? throw new ArgumentNullException(nameof(contextBuilder));
        _actionPlanner = actionPlanner
            ?? throw new ArgumentNullException(nameof(actionPlanner));
        _fallbackDirector = new RuleBasedConversationDirector(_actionPlanner);
    }

    public async Task<ConversationDirectionPlan> CreatePlanAsync(
        AiMessageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ConversationActionPlan baselinePlan = _actionPlanner.CreatePlan(request);

        AiMessageCountRange messageCountRange = GetMessageCountRange(request);
        if (messageCountRange.Maximum == 0)
        {
            return await _fallbackDirector.CreatePlanAsync(
                request,
                cancellationToken);
        }

        string? validationError = null;

        try
        {
            for (int attempt = 0;
                 attempt <= _options.OutputValidationRetryCount;
                 attempt++)
            {
                string userPrompt = BuildUserPrompt(request, baselinePlan);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    userPrompt += Environment.NewLine
                        + $"上一次 json 计划无效：{validationError}"
                        + Environment.NewLine
                        + "请保持业务目标和目标消息不变，重新输出完整 json 对象。";
                }

                string? content = await _chatClient.CompleteJsonAsync(
                    BuildSystemPrompt(),
                    userPrompt,
                    temperature: 0.35,
                    topP: 0.7,
                    maximumCompletionTokens: Math.Min(
                        _options.MaximumCompletionTokens,
                        384),
                    cancellationToken);

                try
                {
                    return ParseAndValidatePlan(content, request, baselinePlan);
                }
                catch (AiMessageGenerationException exception)
                {
                    validationError = exception.Message;
                }
                catch (JsonException)
                {
                    validationError = "返回内容不是可解析的 json 对象。";
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 导演不可用不应阻断既有聊天；安全退回原有规则计划。
        }

        cancellationToken.ThrowIfCancellationRequested();
        return RuleBasedConversationDirector.CreateDirectionPlan(
            request,
            baselinePlan);
    }

    private ConversationDirectionPlan ParseAndValidatePlan(
        string? content,
        AiMessageGenerationRequest request,
        ConversationActionPlan baselinePlan)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiMessageGenerationException("导演没有返回计划内容。");
        }

        using JsonDocument document = JsonDocument.Parse(
            RemoveMarkdownCodeFence(content.Trim()));
        JsonElement root = document.RootElement;

        string actionText = GetRequiredString(root, "action");
        if (!Enum.TryParse(
                actionText,
                ignoreCase: true,
                out ConversationAction action)
            || !Enum.IsDefined(action))
        {
            throw new AiMessageGenerationException("导演返回了不支持的交流动作。");
        }

        if (baselinePlan.Action == ConversationAction.Answer
            && action != ConversationAction.Answer)
        {
            throw new AiMessageGenerationException("目标消息需要直接回答，导演不能改变该硬约束。");
        }

        if (request.Scenario ==
                AiMessageGenerationScenario.AutonomousPrivateChatClosing
            && action != ConversationAction.Close)
        {
            throw new AiMessageGenerationException("收束轮必须使用 Close 动作。");
        }

        string beatText = GetRequiredString(root, "beat");
        if (!Enum.TryParse(
                beatText,
                ignoreCase: true,
                out ConversationBeat beat)
            || !Enum.IsDefined(beat))
        {
            throw new AiMessageGenerationException("导演返回了不支持的会话节拍。");
        }

        if (request.Scenario ==
                AiMessageGenerationScenario.AutonomousPrivateChatClosing
            && beat != ConversationBeat.Close)
        {
            throw new AiMessageGenerationException("收束轮必须使用 Close 会话节拍。");
        }

        Guid requiredTargetMessageId =
            request.ReplyTarget?.Message?.MessageId ?? Guid.Empty;
        string targetMessageIdText = GetRequiredString(
            root,
            "targetMessageId",
            allowEmpty: true);
        Guid returnedTargetMessageId = string.IsNullOrWhiteSpace(targetMessageIdText)
            ? Guid.Empty
            : Guid.TryParse(targetMessageIdText, out Guid parsedTargetId)
                ? parsedTargetId
                : throw new AiMessageGenerationException("导演返回的目标消息 Id 无效。");

        if (returnedTargetMessageId != requiredTargetMessageId)
        {
            throw new AiMessageGenerationException("导演不能更换业务层指定的目标消息。");
        }

        string topicFocus = ValidateLength(
            GetRequiredString(root, "topicFocus"),
            "话题焦点",
            DirectorTopicMaximumLength);
        string responseGoal = ValidateLength(
            GetRequiredString(root, "responseGoal"),
            "回应目标",
            DirectorGoalMaximumLength);
        string newContribution = ValidateLength(
            GetRequiredString(root, "newContribution"),
            "新增内容要求",
            DirectorGoalMaximumLength);
        IReadOnlyList<string> coveredPoints = ParseStringList(
            root,
            "coveredPoints");
        IReadOnlyList<string> unresolvedGoals = ParseStringList(
            root,
            "unresolvedGoals");
        IReadOnlyList<string> avoidedTopics = ParseStringList(
            root,
            "avoidedTopics");
        IReadOnlyList<string> forbiddenClaims = ParseStringList(
            root,
            "forbiddenClaims");
        int selectedMessageCount = GetRequiredInt(root, "messageCount");
        AiMessageCountRange allowedRange = GetMessageCountRange(request);
        if (!allowedRange.Contains(selectedMessageCount))
        {
            throw new AiMessageGenerationException(
                $"导演选择的消息数量必须在 {allowedRange.Minimum} 到 {allowedRange.Maximum} 之间。");
        }

        return new ConversationDirectionPlan(
            _actionPlanner.CreatePlan(request, action),
            beat,
            topicFocus,
            responseGoal,
            returnedTargetMessageId,
            coveredPoints,
            unresolvedGoals,
            newContribution,
            avoidedTopics,
            forbiddenClaims,
            false,
            selectedMessageCount);
    }

    private static IReadOnlyList<string> ParseStringList(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.Array)
        {
            throw new AiMessageGenerationException(
                $"导演计划缺少 {propertyName} 数组。");
        }

        List<string> topics = element
            .EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString()?.Trim() ?? string.Empty
                : string.Empty)
            .ToList();

        if (topics.Count > DirectorListMaximumCount
            || topics.Any(string.IsNullOrWhiteSpace)
            || topics.Any(topic => topic.Length > DirectorListItemMaximumLength))
        {
            throw new AiMessageGenerationException(
                $"导演返回的 {propertyName} 列表无效。");
        }

        return topics
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private string BuildUserPrompt(
        AiMessageGenerationRequest request,
        ConversationActionPlan baselinePlan)
    {
        AiConversationContext context = _contextBuilder.Build(
            request,
            _options.RecentMessageLimit);
        Guid targetMessageId = request.ReplyTarget?.Message?.MessageId
            ?? Guid.Empty;
        StringBuilder builder = new();
        builder.AppendLine(
            $"场景：{AiConversationScenarioPrompt.GetDescription(request.Scenario)}");
        builder.AppendLine("场景事实边界：");
        foreach (string instruction in
                 AiConversationScenarioPrompt.GetBoundaryInstructions(request))
        {
            builder.AppendLine($"- {instruction}");
        }
        builder.AppendLine($"发言者：{request.Speaker.Nickname}");
        builder.AppendLine($"身份：{DisplayOrDefault(request.Speaker.IdentityDescription)}");
        builder.AppendLine($"性格：{DisplayOrDefault(request.Speaker.Personality)}");
        builder.AppendLine($"说话方式：{DisplayOrDefault(request.Speaker.SpeakingStyle)}");
        builder.AppendLine(
            $"其他参与者：{string.Join("、", request.OtherParticipants.Select(account => account.Nickname))}");
        builder.AppendLine($"当前话题：{DisplayOrDefault(request.Topic)}");
        builder.AppendLine($"规则基线动作：{baselinePlan.Action}");
        builder.AppendLine($"关系距离：{baselinePlan.RelationshipTone}");
        builder.AppendLine($"关系投入对比：{baselinePlan.RelationshipBalance}");
        AiMessageCountRange messageCountRange = GetMessageCountRange(request);
        builder.AppendLine(
            messageCountRange.Minimum == messageCountRange.Maximum
                ? $"本轮消息数量：固定 {messageCountRange.Minimum} 条"
                : $"本轮允许消息数量：{messageCountRange.Minimum} 到 {messageCountRange.Maximum} 条，由你根据回应完整性和自然聊天节奏选择");
        builder.AppendLine($"本轮目标消息 Id：{(targetMessageId == Guid.Empty ? "" : targetMessageId)}");
        builder.AppendLine("整轮互动的原始起点（后续发言仍须处理其中未完成的要求）：");
        AppendContextMessage(
            builder,
            request.ConversationAnchor is null
                ? null
                : new AiConversationContextMessage(
                    request.ConversationAnchor,
                    request.ConversationAnchor.SenderType == MessageSenderType.User
                        ? AiConversationMessageOwnership.LocalUser
                        : request.ConversationAnchor.SenderAiAccountId == request.Speaker.Id
                            ? AiConversationMessageOwnership.CurrentSpeaker
                            : AiConversationMessageOwnership.OtherAiAccount));
        builder.AppendLine("本轮必须回应的目标消息：");
        AppendContextMessage(builder, context.ReplyTarget);
        builder.AppendLine("更早背景（不能替代上面的目标消息）：");

        if (context.Messages.Count == 0)
        {
            builder.AppendLine("（暂无）");
        }

        foreach (AiConversationContextMessage message in context.Messages)
        {
            AppendContextMessage(builder, message);
        }

        AppendMemories(builder, context.Memories);

        builder.AppendLine("先从整段会话判断已经表达过什么、原始要求还有什么没完成，再选择一个会话节拍和动作。");
        builder.AppendLine("newContribution 必须指出本轮相对最近消息新增的内容，不能只是换一种说法赞同上一句。");
        builder.AppendLine("forbiddenClaims 必须包含当前好友没有资料或本人历史依据、因此不能声称亲历的事实类型。");
        builder.AppendLine("messageCount 必须在允许范围内；简单反应通常一条，需要解释或包含多个关联信息时可以自然拆成多条，不能为了凑数切碎同一句话。");
        builder.AppendLine("用户明确要求多说几句、分开说或不要只回一句时，只要允许范围容纳，messageCount 至少选择 2。");
        return builder.ToString();
    }

    private static string BuildSystemPrompt() =>
        string.Join(
            Environment.NewLine,
            "你是 VocaChat 的对话导演，只制定单轮语义计划，不编写任何可见聊天台词。",
            "业务层已经确定发言者、参与者、允许的消息数量范围、轮次和目标消息；你绝不能改变这些事实。",
            "可选 action 只有 Acknowledge、Answer、Ask、Share、React、Comfort、Tease、Disagree、Evade、ShiftTopic、Close。",
            "可选 beat 只有 Introduce、Develop、Contrast、Clarify、Resolve、Close，用来表示整段会话的推进位置。",
            "如果规则基线动作为 Answer 或 Close，必须保持该动作。",
            "targetMessageId 必须原样返回；没有目标消息时返回空字符串。",
            "topicFocus 和 responseGoal 应具体、简短，不得包含最终聊天台词。",
            "coveredPoints 列出最近已经说清楚的观点；unresolvedGoals 列出原始要求中仍待完成的部分。",
            "newContribution 指定本轮必须新增的观点、决定、信息或情绪变化，不能只要求附和或复述。",
            "avoidedTopics 列出不要恢复的旧话题；forbiddenClaims 列出没有身份资料或本人历史依据、不可虚构的第一人称事实。",
            "长期记忆只代表当前发言者过去对当前对象形成的认识。只有与本轮目标自然相关时才能用于规划，不能让记忆取代目标消息，也不能把对方经历归到当前发言者名下。",
            "messageCount 表示本轮应独立发送几条聊天消息，必须处于业务层允许范围内。消息数量服务于完整表达和自然节奏，不用于机械切句。",
            "四个数组没有内容时返回空数组，每个数组最多五项。",
            "严格输出 json 对象，不要输出 Markdown 或额外解释。",
            "json 示例：{\"action\":\"Answer\",\"beat\":\"Clarify\",\"topicFocus\":\"对方询问的时间\",\"responseGoal\":\"给出明确时间\",\"messageCount\":2,\"targetMessageId\":\"00000000-0000-0000-0000-000000000000\",\"coveredPoints\":[],\"unresolvedGoals\":[\"回答具体时间\"],\"newContribution\":\"给出尚未出现的具体时间\",\"avoidedTopics\":[],\"forbiddenClaims\":[\"没有本人历史依据的昨晚行程\"]}");

    private static AiMessageCountRange GetMessageCountRange(
        AiMessageGenerationRequest request) =>
        request.AllowedMessageCountRange
        ?? new AiMessageCountRange(
            request.ExpectedMessageCount,
            request.ExpectedMessageCount);

    private static int GetRequiredInt(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out int value))
        {
            throw new AiMessageGenerationException(
                $"导演计划缺少有效的 {propertyName} 数字。");
        }

        return value;
    }

    private static void AppendContextMessage(
        StringBuilder builder,
        AiConversationContextMessage? contextMessage)
    {
        if (contextMessage is null)
        {
            builder.AppendLine("（无具体目标消息）");
            return;
        }

        AiDialogueMessage message = contextMessage.Message;
        builder.AppendLine(
            $"[{contextMessage.Ownership}] {message.SenderDisplayName}：{Truncate(message.Content, 400)}");
    }

    private static void AppendMemories(
        StringBuilder builder,
        IReadOnlyList<AiConversationMemory> memories)
    {
        builder.AppendLine("当前发言者对对话对象的长期记忆（仅作背景）：");

        if (memories.Count == 0)
        {
            builder.AppendLine("（暂无）");
            return;
        }

        foreach (AiConversationMemory memory in memories)
        {
            builder.AppendLine(
                $"[{memory.Type}] 关于{memory.SubjectDisplayName}："
                + $"{Truncate(memory.Summary, 300)}"
                + $"（{memory.OccurredAt:yyyy-MM-dd}）");
        }
    }

    private static string GetRequiredString(
        JsonElement root,
        string propertyName,
        bool allowEmpty = false)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            throw new AiMessageGenerationException($"导演计划缺少 {propertyName}。");
        }

        string value = element.GetString()?.Trim() ?? string.Empty;
        if (!allowEmpty && string.IsNullOrWhiteSpace(value))
        {
            throw new AiMessageGenerationException($"导演计划中的 {propertyName} 不能为空。");
        }

        return value;
    }

    private static string ValidateLength(
        string value,
        string fieldName,
        int maximumLength)
    {
        if (value.Length > maximumLength)
        {
            throw new AiMessageGenerationException(
                $"导演计划中的{fieldName}不能超过 {maximumLength} 个字符。");
        }

        return value;
    }

    private static string RemoveMarkdownCodeFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        int firstLineEnd = value.IndexOf('\n');
        int closingFence = value.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineEnd >= 0 && closingFence > firstLineEnd
            ? value[(firstLineEnd + 1)..closingFence].Trim()
            : value;
    }

    private static string DisplayOrDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "未填写" : value.Trim();

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : $"{value[..maximumLength]}…";
}

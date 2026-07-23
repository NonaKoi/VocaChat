using System.Text;
using System.Text.Json;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 使用全局 OpenAI 兼容模型为一次用户群聊制定发言顺序和语义分工。
/// 模型不可用或计划越界时，退回现有规则选择逻辑。
/// </summary>
public sealed class OpenAiCompatibleGroupConversationDirector
    : IGroupConversationDirector
{
    private const int MaximumCandidateCount = 8;
    private readonly OpenAiCompatibleChatClient _chatClient;
    private readonly AiMessageGenerationOptions _options;
    private readonly GroupChatReplyPlanner _replyPlanner;
    private readonly GroupConversationPlanValidator _validator;
    private readonly GroupConversationContextService _conversationContextService;
    private readonly RuleBasedGroupConversationDirector _fallbackDirector;

    public OpenAiCompatibleGroupConversationDirector(
        OpenAiCompatibleChatClient chatClient,
        AiMessageGenerationOptions options,
        GroupChatReplyPlanner replyPlanner,
        GroupConversationPlanValidator validator,
        GroupConversationContextService conversationContextService)
    {
        _chatClient = chatClient
            ?? throw new ArgumentNullException(nameof(chatClient));
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
        _replyPlanner = replyPlanner
            ?? throw new ArgumentNullException(nameof(replyPlanner));
        _validator = validator
            ?? throw new ArgumentNullException(nameof(validator));
        _conversationContextService = conversationContextService
            ?? throw new ArgumentNullException(
                nameof(conversationContextService));
        _fallbackDirector = new RuleBasedGroupConversationDirector(
            _replyPlanner);
    }

    public async Task<GroupConversationTurnPlan> CreatePlanAsync(
        GroupConversationPlanningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        GroupConversationTurnPlan fallbackPlan =
            await _fallbackDirector.CreatePlanAsync(request, cancellationToken);

        if (fallbackPlan.Speakers.Count == 0)
        {
            return fallbackPlan;
        }

        IReadOnlyList<AiAccount> candidatePool = ResolveCandidatePool(request);
        HashSet<Guid> candidateIds = candidatePool
            .Select(account => account.Id)
            .ToHashSet();
        string? validationError = null;

        try
        {
            IReadOnlyList<GroupConversationCandidateContext>
                candidateContexts = _conversationContextService
                    .BuildCandidateContexts(
                        request,
                        candidatePool,
                        fallbackPlan);
            for (int attempt = 0;
                 attempt <= _options.OutputValidationRetryCount;
                 attempt++)
            {
                string userPrompt = BuildUserPrompt(
                    request,
                    candidatePool,
                    fallbackPlan,
                    candidateContexts);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    userPrompt += Environment.NewLine
                        + $"上一次群聊计划无效：{validationError}"
                        + Environment.NewLine
                        + "请保持用户锚点和候选成员不变，重新输出完整 json 对象。";
                }

                string? content = await _chatClient.CompleteJsonAsync(
                    BuildSystemPrompt(request.Scenario),
                    userPrompt,
                    temperature: 0.25,
                    topP: 0.65,
                    maximumCompletionTokens: Math.Min(
                        _options.MaximumCompletionTokens,
                        768),
                    cancellationToken,
                    invocationContext: request.UsageCorrelation
                        ?.CreateInvocationContext(
                            AiModelInvocationStage.GroupDirector,
                            attempt + 1));

                try
                {
                    GroupConversationTurnPlan plan = ParsePlan(
                        content,
                        request,
                        fallbackPlan.SelectionStatus,
                        candidateIds);
                    if (_validator.TryValidate(
                            request,
                            plan,
                            out string planError))
                    {
                        return plan;
                    }

                    validationError = planError;
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
            // 群级模型导演不可用不应阻断聊天，继续执行规则回退计划。
        }

        cancellationToken.ThrowIfCancellationRequested();
        return fallbackPlan;
    }

    private static GroupConversationTurnPlan ParsePlan(
        string? content,
        GroupConversationPlanningRequest request,
        AiSpeakerSelectionStatus selectionStatus,
        IReadOnlySet<Guid> candidateIds)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiMessageGenerationException(
                "群聊导演没有返回计划内容。");
        }

        using JsonDocument document = JsonDocument.Parse(
            RemoveMarkdownCodeFence(content.Trim()));
        JsonElement root = document.RootElement;
        JsonElement speakerItems = GetRequiredArray(root, "speakers");
        List<GroupConversationSpeakerPlan> speakers = new();

        foreach (JsonElement item in speakerItems.EnumerateArray())
        {
            Guid speakerId = GetRequiredGuid(item, "speakerAiAccountId");
            if (!candidateIds.Contains(speakerId))
            {
                throw new AiMessageGenerationException(
                    "群聊导演选择了候选池之外的发言者。");
            }

            speakers.Add(new GroupConversationSpeakerPlan
            {
                SpeakerAiAccountId = speakerId,
                ReplyTargetMessageId = GetOptionalGuid(
                    item,
                    "replyTargetMessageId"),
                TargetAiAccountId = GetOptionalGuid(
                    item,
                    "targetAiAccountId"),
                Audience = GetRequiredEnum<GroupConversationAudience>(
                    item,
                    "audience"),
                Role = GetRequiredEnum<GroupConversationRole>(item, "role"),
                ResponseGoal = GetRequiredString(item, "responseGoal"),
                NewContribution = GetRequiredString(item, "newContribution"),
                AvoidedRepetition = GetStringList(
                    item,
                    "avoidedRepetition")
            });
        }

        return new GroupConversationTurnPlan
        {
            AnchorMessageId = request.AnchorMessage?.Id,
            TopicFocus = GetRequiredString(root, "topicFocus"),
            TurnGoal = GetRequiredString(root, "turnGoal"),
            CoveredPoints = GetStringList(root, "coveredPoints"),
            UnresolvedGoals = GetStringList(root, "unresolvedGoals"),
            Speakers = speakers.AsReadOnly(),
            SelectionStatus = selectionStatus,
            UsedRuleFallback = false
        };
    }

    private static string BuildUserPrompt(
        GroupConversationPlanningRequest request,
        IReadOnlyList<AiAccount> candidatePool,
        GroupConversationTurnPlan fallbackPlan,
        IReadOnlyList<GroupConversationCandidateContext> candidateContexts)
    {
        if (request.Scenario != GroupConversationPlanningScenario.UserMessage)
        {
            return BuildAutonomousUserPrompt(
                request,
                candidatePool,
                fallbackPlan,
                candidateContexts);
        }

        GroupMessage anchorMessage = request.AnchorMessage
            ?? throw new ArgumentException(
                "用户群聊计划缺少用户锚点消息。",
                nameof(request));
        StringBuilder builder = new();
        builder.AppendLine($"群聊：{request.GroupChat.Name}");
        builder.AppendLine($"群聊 Id：{request.GroupChat.Id}");
        builder.AppendLine($"用户锚点消息 Id：{anchorMessage.Id}");
        builder.AppendLine($"用户消息：{anchorMessage.Content}");
        builder.AppendLine($"最多发言者：{request.MaximumSpeakerCount}");
        builder.AppendLine($"最多 AI 消息总数：{request.MaximumTotalMessageCount}");
        builder.AppendLine($"点名识别状态：{fallbackPlan.SelectionStatus}");
        builder.AppendLine("允许选择的候选成员：");

        foreach (AiAccount account in candidatePool)
        {
            GroupConversationCandidateContext? context = candidateContexts
                .SingleOrDefault(item => item.AiAccountId == account.Id);
            builder.AppendLine(
                $"- Id={account.Id}; 昵称={account.Nickname}; "
                + $"身份={DisplayOrDefault(account.IdentityDescription)}; "
                + $"性格={DisplayOrDefault(account.Personality)}; "
                + $"说话方式={DisplayOrDefault(account.SpeakingStyle)}; "
                + $"职业={DisplayOrDefault(account.Occupation)}; "
                + $"标签={FormatTags(account)}");
            AppendCandidateContext(builder, account, context);
        }

        builder.AppendLine("最近群聊消息（只能引用这里或用户锚点中的消息 Id）：");
        foreach (GroupMessage message in request.RecentMessages.TakeLast(12))
        {
            builder.AppendLine(
                $"- Id={message.Id}; 发送者={message.SenderDisplayName}; "
                + $"发送者AI Id={message.SenderAiAccountId?.ToString() ?? "本地用户"}; "
                + $"内容={message.Content}");
        }

        builder.AppendLine("现有规则的安全建议（可调整语义分工，但不可越过候选池和数量边界）：");
        foreach (GroupConversationSpeakerPlan speaker in fallbackPlan.Speakers)
        {
            builder.AppendLine(
                $"- 发言者={speaker.SpeakerAiAccountId}; "
                + $"职责={speaker.Role}; 新增内容={speaker.NewContribution}");
        }

        builder.AppendLine("通常只选择一位最合适的成员；只有确实存在不同且有价值的贡献时才选择两到三位。不得让所有成员轮流报到。没有点名冲突时，如果某位候选人的资料或本人记忆与当前话题明确匹配，应优先于只有宽泛职业或兴趣关联的候选人。允许后发者面向本地用户、整个群，或回应本轮更早安排的一位 AI。每位发言者只能承担一个主要贡献，newContribution 之间不得重复。多人被精确点名且结论相近时，第一位负责核心结论；后续成员必须回应更早发言者，并补充不同的权衡、实施建议、例外或细节，不能并列重复同一结论和理由。精确 @ 到群成员时必须优先选择被点名者。未匹配的点名不能选择群外账号。replyTargetMessageId 必须使用上面已存在的消息 Id；如果要回应本轮更早安排的一位 AI，replyTargetMessageId 仍填写用户锚点，另用 targetAiAccountId 指明该 AI。个人记忆只属于标明的候选好友；方向记忆只表示该候选好友对指定对象的认识，不能转给其他候选人。关系亲近只允许改变表达距离和互动方式，不能据此虚构双方过去共同做过的事、固定相处习惯或反复发生的行为。newContribution 中的个人经历、熟悉地点、收藏、既定偏好和过往互动必须能在资料、本人记忆或最近真实消息中找到依据；没有依据时只能写成当下建议、设想或意愿，且不得与已有记忆冲突。输出只包含语义计划，不写最终聊天台词。");
        return builder.ToString();
    }

    private static string BuildAutonomousUserPrompt(
        GroupConversationPlanningRequest request,
        IReadOnlyList<AiAccount> candidatePool,
        GroupConversationTurnPlan fallbackPlan,
        IReadOnlyList<GroupConversationCandidateContext> candidateContexts)
    {
        StringBuilder builder = new();
        builder.AppendLine($"好友群聊：{request.GroupChat.Name}");
        builder.AppendLine($"群聊 Id：{request.GroupChat.Id}");
        builder.AppendLine($"规划场景：{request.Scenario}");
        builder.AppendLine($"当前话题：{ResolveTopic(request)}");
        builder.AppendLine(
            $"锚点消息 Id：{request.AnchorMessage?.Id.ToString() ?? "无（首次自然开场）"}");
        builder.AppendLine(
            $"锚点内容：{request.AnchorMessage?.Content ?? "尚未产生消息，由发起者自然引入话题"}");
        builder.AppendLine($"最多发言者：{request.MaximumSpeakerCount}");
        builder.AppendLine($"最大消息总数：{request.MaximumTotalMessageCount}");
        if (request.RequiredSpeakerAiAccountId is Guid requiredSpeakerId)
        {
            builder.AppendLine($"必须排在第一位的发起者 Id：{requiredSpeakerId}");
        }

        builder.AppendLine("允许选择的候选成员：");
        foreach (AiAccount account in candidatePool)
        {
            GroupConversationCandidateContext? context = candidateContexts
                .SingleOrDefault(item => item.AiAccountId == account.Id);
            builder.AppendLine(
                $"- Id={account.Id}; 昵称={account.Nickname}; "
                + $"身份={DisplayOrDefault(account.IdentityDescription)}; "
                + $"性格={DisplayOrDefault(account.Personality)}; "
                + $"说话方式={DisplayOrDefault(account.SpeakingStyle)}; "
                + $"职业={DisplayOrDefault(account.Occupation)}; "
                + $"标签={FormatTags(account)}");
            AppendCandidateContext(builder, account, context);
        }

        builder.AppendLine("最近真实群聊消息：");
        foreach (GroupMessage message in request.RecentMessages.TakeLast(12))
        {
            builder.AppendLine(
                $"- Id={message.Id}; 发送者={message.SenderDisplayName}; "
                + $"发送者 AI Id={message.SenderAiAccountId?.ToString() ?? "无"}; "
                + $"内容={message.Content}");
        }

        builder.AppendLine("规则回退建议：");
        foreach (GroupConversationSpeakerPlan speaker in fallbackPlan.Speakers)
        {
            builder.AppendLine(
                $"- 发言者={speaker.SpeakerAiAccountId}; "
                + $"受众={speaker.Audience}; 职责={speaker.Role}; "
                + $"新增内容={speaker.NewContribution}");
        }

        builder.AppendLine(request.Scenario switch
        {
            GroupConversationPlanningScenario.AutonomousOpening =>
                "这是第一轮。第一位必须是指定发起者，由其像平常聊天一样自然说起话题，不能使用主持、宣布、欢迎大家讨论等口吻。开场只安排发起者表达当下形成的看法、偏好或提议；没有本人资料或记忆支持时，不得替发起者虚构此前经历、当前行程或既定个人安排。后续只选择确实会对发起者实际内容产生不同反应的少量成员。开场发起者没有已存在的 replyTargetMessageId，应输出 null；面向全群时 audience 使用 WholeGroup。",
            GroupConversationPlanningScenario.AutonomousClosing =>
                "这是唯一收束轮。可以选择零至两位成员，只能接住最后内容、简短回应或自然结束；不得提出新问题、转移话题或制造必须继续的悬念。零人结束时 speakers 输出空数组。",
            _ =>
                "这是概率已经通过后的后续轮。根据最近真实消息选择一至三位确实有新贡献的成员，不要轮流报到。优先回应具体成员的实际消息；允许同意、反对、补充、安慰或调侃，但每个人必须承担不同贡献。"
        });
        builder.AppendLine(
            "好友自主群聊中 audience 只能是 SpecificAiAccount 或 WholeGroup，绝不能是 LocalUser。SpecificAiAccount 必须指向当前群成员，并优先引用该成员已有的真实消息。关系和记忆均具有明确所有者，不得把一位成员的经历分配给另一位成员。输出只包含语义计划，不写最终聊天台词。");
        return builder.ToString();
    }

    private static string BuildSystemPrompt(
        GroupConversationPlanningScenario scenario) => string.Join(
        Environment.NewLine,
        scenario == GroupConversationPlanningScenario.UserMessage
            ? "你是 VocaChat 的用户群聊协调导演，只决定本轮由谁发言、面向谁、承担什么职责以及新增什么内容。"
            : "你是 VocaChat 的好友自主群聊协调导演，只决定本轮由谁发言、面向谁、承担什么职责以及新增什么内容。",
        "你不编写聊天台词，不改变群成员，不创建账号，也不决定每位发言者发送几条消息。",
        "speakerAiAccountId 只能来自候选成员，最多选择业务层允许的数量，且同一成员只能出现一次。",
        scenario == GroupConversationPlanningScenario.UserMessage
            ? "audience 只能是 LocalUser、SpecificAiAccount、WholeGroup。"
            : "这是不包含本地用户的好友自主群聊，audience 只能是 SpecificAiAccount 或 WholeGroup，绝不能输出 LocalUser。",
        "role 只能是 DirectAnswer、Complement、AgreeAndExtend、Disagree、React、Comfort、Clarify、Tease、ShiftTopic、Close。",
        "SpecificAiAccount 必须提供 targetAiAccountId，且只能指向最近历史中已经发言或本计划中更早安排的群成员。其他 audience 的 targetAiAccountId 必须为 null。",
        scenario == GroupConversationPlanningScenario.UserMessage
            ? "精确点名优先；普通消息默认一位发言者；只有不同成员确实能提供不同价值时才安排两到三位。"
            : "根据真实内容选择一至三位有新增贡献的成员；不得为了热闹让所有成员轮流发言。",
        "不得安排附和复读。每位发言者必须有不同的 newContribution，并在 avoidedRepetition 中写出需要避免复述的已表达内容。newContribution 应描述发言角度和需要增加的信息类型，不要写成带有大量具体细节的台词草稿。",
        "多人被点名且立场相同时，第一位负责共同结论，后续成员必须回应更早发言者并增加不同的权衡、行动建议、例外或细节；不得让多位成员并列重复同一结论和理由。",
        "每位候选人的个人记忆、经历和方向记忆都有明确所有者。只能据此判断该候选人是否适合发言，绝不能把一位成员的事实分配给另一位成员。",
        "当某位候选人的明确资料或本人记忆与当前话题高度匹配时，在不违反点名和数量规则的前提下优先选择该候选人。宽泛的职业或兴趣关联不能替代更直接的事实依据。",
        "关系亲近只调节语气、距离和互动意愿，不证明双方存在未记录的共同经历、固定习惯或反复发生的互动。newContribution 不得把推测写成个人事实；只能原样使用已提供事实或在省略细节后做更抽象的概括，不能从宽泛资料推导出具体地点、物品、事件、感官细节或相处习惯。没有资料、本人记忆或最近真实消息支持时，只能提出当下建议、设想或意愿，并且不得与已有记忆冲突。",
        scenario == GroupConversationPlanningScenario.UserMessage
            ? "用户提供的事实只属于用户或用户明确指向的对象，不得改写成候选成员自己的经历。"
            : "预设话题是共同讨论情境，只能据此安排成员表达即时观点、偏好或提议；不得把话题扩写成成员此前经历、当前行程或既定个人安排，除非该成员的资料或本人记忆明确支持。",
        "关系具有方向。候选人到回应对象与回应对象到候选人的分数可能不同；关系只调节距离、立场和互动方式，不替代事实依据。",
        "严格输出 json 对象，不要输出 Markdown 或额外解释。",
        scenario == GroupConversationPlanningScenario.UserMessage
            ? "json 示例：{\"topicFocus\":\"周末去哪\",\"turnGoal\":\"回答用户并补充不同偏好\",\"coveredPoints\":[],\"unresolvedGoals\":[\"给出可选地点\"],\"speakers\":[{\"speakerAiAccountId\":\"00000000-0000-0000-0000-000000000000\",\"replyTargetMessageId\":\"00000000-0000-0000-0000-000000000000\",\"targetAiAccountId\":null,\"audience\":\"LocalUser\",\"role\":\"DirectAnswer\",\"responseGoal\":\"直接回答用户\",\"newContribution\":\"提出一个具体去处\",\"avoidedRepetition\":[]}]}"
            : "自主开场 json 示例：{\"topicFocus\":\"周末去哪\",\"turnGoal\":\"由发起者自然说起周末去处\",\"coveredPoints\":[],\"unresolvedGoals\":[],\"speakers\":[{\"speakerAiAccountId\":\"00000000-0000-0000-0000-000000000000\",\"replyTargetMessageId\":null,\"targetAiAccountId\":null,\"audience\":\"WholeGroup\",\"role\":\"ShiftTopic\",\"responseGoal\":\"自然引入话题\",\"newContribution\":\"提出一个当下想到的周末去处\",\"avoidedRepetition\":[]}]}");

    private IReadOnlyList<AiAccount> ResolveCandidatePool(
        GroupConversationPlanningRequest request)
    {
        if (request.Scenario == GroupConversationPlanningScenario.UserMessage)
        {
            GroupMessage anchorMessage = request.AnchorMessage
                ?? throw new ArgumentException(
                    "用户群聊计划缺少用户锚点消息。",
                    nameof(request));
            return _replyPlanner.CreateCandidatePool(
                request.GroupChat,
                anchorMessage.Content,
                MaximumCandidateCount);
        }

        Dictionary<Guid, AiAccount> members = request.GroupChat.Members
            .ToDictionary(member => member.Id);
        IEnumerable<Guid> recentSpeakerIds = request.RecentMessages
            .Where(message => message.SenderAiAccountId is not null)
            .Reverse()
            .Select(message => message.SenderAiAccountId!.Value);
        IEnumerable<Guid> candidateIds = request.RequiredSpeakerAiAccountId is
            Guid requiredSpeakerId
                ? new[] { requiredSpeakerId }
                    .Concat(request.PreferredSpeakerAiAccountIds)
                    .Concat(recentSpeakerIds)
                    .Concat(members.Keys)
                : request.PreferredSpeakerAiAccountIds
                    .Concat(recentSpeakerIds)
                    .Concat(members.Keys);

        return candidateIds
            .Distinct()
            .Where(members.ContainsKey)
            .Take(MaximumCandidateCount)
            .Select(id => members[id])
            .ToList()
            .AsReadOnly();
    }

    private static string ResolveTopic(
        GroupConversationPlanningRequest request) =>
        !string.IsNullOrWhiteSpace(request.Topic)
            ? request.Topic.Trim()
            : request.AnchorMessage?.Content.Trim()
                ?? "当前群聊话题";

    private static void AppendCandidateContext(
        StringBuilder builder,
        AiAccount account,
        GroupConversationCandidateContext? context)
    {
        builder.AppendLine($"  {account.Nickname}本人的相关个人记忆：");
        if (context is null || context.RelevantSelfMemories.Count == 0)
        {
            builder.AppendLine("  - （暂无）");
        }
        else
        {
            foreach (AiConversationSelfMemory memory in context
                         .RelevantSelfMemories)
            {
                builder.AppendLine(
                    $"  - 所有者={account.Nickname}; 类型={memory.Type}; "
                    + $"内容={Truncate(memory.Summary, 180)}");
            }
        }

        builder.AppendLine($"  {account.Nickname}与潜在回应对象的方向上下文：");
        if (context is null || context.Relationships.Count == 0)
        {
            builder.AppendLine("  - （没有可靠的 AI 关系对象）");
            return;
        }

        foreach (GroupConversationRelationshipContext relationship in
                 context.Relationships)
        {
            builder.AppendLine(
                $"  - {account.Nickname}→{relationship.TargetDisplayName}="
                + $"{relationship.SpeakerToTargetScore:0.##}; "
                + $"{relationship.TargetDisplayName}→{account.Nickname}="
                + $"{relationship.TargetToSpeakerScore:0.##}");
            foreach (AiConversationMemory memory in relationship
                         .RelevantMemories)
            {
                builder.AppendLine(
                    $"    方向记忆所有者={account.Nickname}; "
                    + $"对象={relationship.TargetDisplayName}; "
                    + $"内容={Truncate(memory.Summary, 180)}");
            }
        }
    }

    private static string FormatTags(AiAccount account)
    {
        IReadOnlyList<string> tags = account.Tags
            .Select(tag => tag.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        return tags.Count == 0 ? "未填写" : string.Join("、", tags);
    }

    private static string DisplayOrDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "未填写" : value.Trim();

    private static string Truncate(string value, int maximumLength)
    {
        string normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : $"{normalized[..(maximumLength - 1)]}…";
    }

    private static string RemoveMarkdownCodeFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        int firstLineEnd = value.IndexOf('\n');
        int lastFence = value.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineEnd >= 0 && lastFence > firstLineEnd
            ? value[(firstLineEnd + 1)..lastFence].Trim()
            : value;
    }

    private static JsonElement GetRequiredArray(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array)
        {
            throw new AiMessageGenerationException(
                $"群聊导演缺少数组字段 {propertyName}。");
        }

        return value;
    }

    private static string GetRequiredString(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new AiMessageGenerationException(
                $"群聊导演缺少文本字段 {propertyName}。");
        }

        return value.GetString()!.Trim();
    }

    private static Guid GetRequiredGuid(
        JsonElement root,
        string propertyName)
    {
        string value = GetRequiredString(root, propertyName);
        if (!Guid.TryParse(value, out Guid id))
        {
            throw new AiMessageGenerationException(
                $"群聊导演字段 {propertyName} 不是有效 Guid。");
        }

        return id;
    }

    private static Guid? GetOptionalGuid(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String
            || !Guid.TryParse(value.GetString(), out Guid id))
        {
            throw new AiMessageGenerationException(
                $"群聊导演字段 {propertyName} 不是有效的可空 Guid。");
        }

        return id;
    }

    private static TEnum GetRequiredEnum<TEnum>(
        JsonElement root,
        string propertyName)
        where TEnum : struct, Enum
    {
        string value = GetRequiredString(root, propertyName);
        if (!Enum.TryParse(value, ignoreCase: true, out TEnum result)
            || !Enum.IsDefined(result))
        {
            throw new AiMessageGenerationException(
                $"群聊导演字段 {propertyName} 的取值无效。");
        }

        return result;
    }

    private static IReadOnlyList<string> GetStringList(
        JsonElement root,
        string propertyName)
    {
        JsonElement array = GetRequiredArray(root, propertyName);
        List<string> values = new();
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new AiMessageGenerationException(
                    $"群聊导演数组 {propertyName} 包含无效文本。");
            }

            values.Add(item.GetString()!.Trim());
        }

        return values.AsReadOnly();
    }
}

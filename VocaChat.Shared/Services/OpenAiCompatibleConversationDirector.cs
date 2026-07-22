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
    private const int DirectorSelfMemoryProposalMaximumCount = 2;
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
        ConversationActionPlan initialPlan = GroupConversationRoleMapper.Apply(
            _actionPlanner.CreatePlan(request),
            request.GroupConversationPlan);
        ConversationActionPlan baselinePlan = request.QuestionPolicy?.ApplyTo(
            initialPlan) ?? initialPlan;

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
                        512),
                    cancellationToken,
                    aiAccountId: request.Speaker.Id);

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

        ConversationReferencePlan referencePlan = ParseReferencePlan(root);

        if (request.GroupConversationPlan is not null
            && action != baselinePlan.Action)
        {
            throw new AiMessageGenerationException(
                "单人导演不能改变群级导演分配的发言职责。");
        }

        if (baselinePlan.Action == ConversationAction.Answer
            && action != ConversationAction.Answer
            && !(referencePlan.Status == ConversationReferenceStatus.Ambiguous
                && action == ConversationAction.Ask))
        {
            throw new AiMessageGenerationException("目标消息需要直接回答，导演不能改变该硬约束。");
        }

        if (request.QuestionPolicy?.ForceDeclarativeReply == true
            && action == ConversationAction.Ask)
        {
            throw new AiMessageGenerationException(
                "连续疑问轮次已达到上限，本轮不能继续使用 Ask 动作。");
        }

        string questionModeText = GetRequiredString(root, "questionMode");
        if (!Enum.TryParse(
                questionModeText,
                ignoreCase: true,
                out ConversationQuestionMode questionMode)
            || !Enum.IsDefined(questionMode))
        {
            throw new AiMessageGenerationException(
                "导演返回了不支持的疑问句模式。");
        }

        if (request.QuestionPolicy?.ForceDeclarativeReply == true
            && questionMode != ConversationQuestionMode.None)
        {
            throw new AiMessageGenerationException(
                "连续疑问轮次已达到上限，本轮必须使用 None 疑问句模式。");
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

        if (request.Scenario ==
                AiMessageGenerationScenario.AutonomousGroupChat
            && request.GroupConversationPlan is not null
            && !IsAlignedWithAutonomousGroupPlan(
                request,
                topicFocus,
                responseGoal,
                newContribution))
        {
            throw new AiMessageGenerationException(
                "单人导演偏离了群级导演指定的话题或新增内容。");
        }

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
        IReadOnlyList<Guid> referencedSelfMemoryIds = ParseOptionalGuidList(
            root,
            "referencedSelfMemoryIds");
        IReadOnlyList<AiSelfMemoryProposal> selfMemoryProposals =
            ParseOptionalSelfMemoryProposals(root);
        int selectedMessageCount = GetRequiredInt(root, "messageCount");
        AiMessageCountRange allowedRange = GetMessageCountRange(request);
        if (!allowedRange.Contains(selectedMessageCount))
        {
            throw new AiMessageGenerationException(
                $"导演选择的消息数量必须在 {allowedRange.Minimum} 到 {allowedRange.Maximum} 之间。");
        }

        ConversationActionPlan actionPlan = _actionPlanner
            .CreatePlan(request, action) with
        {
            QuestionMode = questionMode
        };
        return new ConversationDirectionPlan(
            request.QuestionPolicy?.ApplyTo(actionPlan) ?? actionPlan,
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
            selectedMessageCount,
            referencedSelfMemoryIds,
            selfMemoryProposals,
            referencePlan);
    }

    private static ConversationReferencePlan ParseReferencePlan(
        JsonElement root)
    {
        if (!root.TryGetProperty(
                "referenceStatus",
                out JsonElement statusElement))
        {
            return ConversationReferencePlan.None;
        }

        if (statusElement.ValueKind != JsonValueKind.String
            || !Enum.TryParse(
                statusElement.GetString(),
                ignoreCase: true,
                out ConversationReferenceStatus status)
            || !Enum.IsDefined(status))
        {
            throw new AiMessageGenerationException(
                "导演返回了不支持的指代解析状态。");
        }

        string resolutionSummary = root.TryGetProperty(
                "referenceResolution",
                out JsonElement resolutionElement)
            && resolutionElement.ValueKind == JsonValueKind.String
                ? resolutionElement.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        IReadOnlyList<string> factOwnership = root.TryGetProperty(
                "factOwnership",
                out _)
            ? ParseStringList(root, "factOwnership")
            : Array.Empty<string>();

        if (resolutionSummary.Length > DirectorGoalMaximumLength)
        {
            throw new AiMessageGenerationException("导演返回的指代解析说明过长。");
        }

        if (status == ConversationReferenceStatus.Resolved
            && (string.IsNullOrWhiteSpace(resolutionSummary)
                || factOwnership.Count == 0))
        {
            throw new AiMessageGenerationException(
                "已解析的指代必须说明对象和相关事实归属。");
        }

        if (status == ConversationReferenceStatus.Ambiguous
            && string.IsNullOrWhiteSpace(resolutionSummary))
        {
            throw new AiMessageGenerationException(
                "无法确定的指代必须说明歧义所在。");
        }

        if (status == ConversationReferenceStatus.None)
        {
            return ConversationReferencePlan.None;
        }

        return new ConversationReferencePlan(
            status,
            resolutionSummary,
            factOwnership);
    }

    private static bool IsAlignedWithAutonomousGroupPlan(
        AiMessageGenerationRequest request,
        string topicFocus,
        string responseGoal,
        string newContribution)
    {
        GroupConversationSpeakerPlan groupPlan =
            request.GroupConversationPlan!;
        IReadOnlyList<string> topicSources = new[]
        {
            request.Topic,
            request.FocusContent,
            groupPlan.ResponseGoal,
            groupPlan.NewContribution
        }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList()
            .AsReadOnly();
        bool topicAligned = topicSources.Any(source =>
            AiFactGroundingMatcher.HasGroundingOverlap(
                topicFocus,
                source));
        string contributionPlan = $"{responseGoal} {newContribution}";
        bool contributionAligned = topicSources.Any(source =>
            AiFactGroundingMatcher.HasGroundingOverlap(
                contributionPlan,
                source));

        return topicAligned && contributionAligned;
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

    private static IReadOnlyList<Guid> ParseOptionalGuidList(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element))
        {
            return Array.Empty<Guid>();
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new AiMessageGenerationException(
                $"导演计划中的 {propertyName} 必须是数组。");
        }

        List<Guid> ids = new();
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || !Guid.TryParse(item.GetString(), out Guid id))
            {
                throw new AiMessageGenerationException(
                    $"导演计划中的 {propertyName} 包含无效 Id。");
            }

            ids.Add(id);
        }

        if (ids.Count > 6)
        {
            throw new AiMessageGenerationException(
                $"导演计划中的 {propertyName} 最多包含 6 项。");
        }

        return ids.Distinct().ToList().AsReadOnly();
    }

    private static IReadOnlyList<AiSelfMemoryProposal>
        ParseOptionalSelfMemoryProposals(JsonElement root)
    {
        if (!root.TryGetProperty(
                "selfMemoryProposals",
                out JsonElement element))
        {
            return Array.Empty<AiSelfMemoryProposal>();
        }

        if (element.ValueKind != JsonValueKind.Array
            || element.GetArrayLength() >
                DirectorSelfMemoryProposalMaximumCount)
        {
            throw new AiMessageGenerationException(
                "导演计划中的个人记忆建议必须是最多两项的数组。");
        }

        List<AiSelfMemoryProposal> proposals = new();
        foreach (JsonElement item in element.EnumerateArray())
        {
            string operationText = GetRequiredString(item, "operation");
            string typeText = GetRequiredString(item, "type");
            if (!Enum.TryParse(
                    operationText,
                    ignoreCase: true,
                    out AiSelfMemoryProposalOperation operation)
                || !Enum.IsDefined(operation)
                || !Enum.TryParse(
                    typeText,
                    ignoreCase: true,
                    out AiSelfMemoryType type)
                || !Enum.IsDefined(type))
            {
                throw new AiMessageGenerationException(
                    "导演返回了不支持的个人记忆操作或类型。");
            }

            Guid? targetMemoryId = null;
            if (item.TryGetProperty(
                    "targetMemoryId",
                    out JsonElement targetElement)
                && targetElement.ValueKind != JsonValueKind.Null)
            {
                if (targetElement.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(
                        targetElement.GetString(),
                        out Guid parsedTargetId))
                {
                    throw new AiMessageGenerationException(
                        "导演返回的目标个人记忆 Id 无效。");
                }

                targetMemoryId = parsedTargetId;
            }

            string summary = GetRequiredString(item, "summary");
            string reason = GetRequiredString(item, "reason");
            if (summary.Length > AiSelfMemory.SummaryMaxLength
                || reason.Length > 200)
            {
                throw new AiMessageGenerationException(
                    "导演返回的个人记忆摘要或原因过长。");
            }

            proposals.Add(new AiSelfMemoryProposal(
                operation,
                targetMemoryId,
                type,
                summary,
                reason));
        }

        return proposals.AsReadOnly();
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
        builder.AppendLine(
            request.RelationshipTarget is null
                ? "本轮 AI 关系对象：无（回应本地用户或面向全群，不能任意套用成员关系）"
                : $"本轮 AI 关系对象：{request.RelationshipTarget.Nickname}（只允许使用当前发言者与该对象之间的方向关系和记忆）");
        if (request.GroupConversationPlan is not null)
        {
            GroupConversationSpeakerPlan groupPlan =
                request.GroupConversationPlan;
            builder.AppendLine("群级导演已确定以下硬约束，不得改变发言对象、职责或新增内容：");
            builder.AppendLine($"- 主要受众：{groupPlan.Audience}");
            builder.AppendLine($"- 群内职责：{groupPlan.Role}");
            builder.AppendLine($"- 回应目标：{groupPlan.ResponseGoal}");
            builder.AppendLine($"- 必须新增：{groupPlan.NewContribution}");
            if (groupPlan.AvoidedRepetition.Count > 0)
            {
                builder.AppendLine(
                    $"- 不得重复：{string.Join("、", groupPlan.AvoidedRepetition)}");
            }
        }
        builder.AppendLine($"当前话题：{DisplayOrDefault(request.Topic)}");
        builder.AppendLine($"规则基线动作：{baselinePlan.Action}");
        builder.AppendLine($"关系距离：{baselinePlan.RelationshipTone}");
        builder.AppendLine($"关系投入对比：{baselinePlan.RelationshipBalance}");
        builder.AppendLine(
            request.QuestionPolicy?.ForceDeclarativeReply == true
                ? $"疑问句策略：此前已经连续 {request.QuestionPolicy.ConsecutiveQuestionTurns} 轮以问题收尾，本轮必须使用 questionMode=None，且不能选择 Ask"
                : "疑问句策略：可根据当前回应目的选择 None、Optional 或 Natural，不需要为了延续对话而强行提问");
        AiMessageCountRange messageCountRange = GetMessageCountRange(request);
        int variationReference = messageCountRange.Minimum
            == messageCountRange.Maximum
                ? messageCountRange.Minimum
                : Random.Shared.Next(
                    messageCountRange.Minimum,
                    messageCountRange.Maximum + 1);
        builder.AppendLine(
            messageCountRange.Minimum == messageCountRange.Maximum
                ? $"本轮消息数量：固定 {messageCountRange.Minimum} 条"
                : $"本轮允许消息数量：{messageCountRange.Minimum} 到 {messageCountRange.Maximum} 条，由你根据回应完整性和自然聊天节奏选择");
        builder.AppendLine(
            $"自然变化参考：{variationReference} 条。先按内容完整性决定；只有多个数量同样合适时才参考此值，不能为凑数拆句。");
        builder.AppendLine($"本轮目标消息 Id：{(targetMessageId == Guid.Empty ? "" : targetMessageId)}");
        builder.AppendLine(
            $"距离上一条历史消息：{AiConversationTimeGapFormatter.Format(context.GapSincePreviousMessage)}");
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

        AppendSelfMemories(builder, context.SelfMemories);
        AppendMemories(builder, context.Memories);

        builder.AppendLine("先从整段会话判断已经表达过什么、原始要求还有什么没完成，再选择一个会话节拍和动作。");
        builder.AppendLine("历史消息中的 CurrentSpeaker 表示当前发言好友，ReplyTargetAiAccount 表示当前具体回应对象，OtherAiAccount 表示其他第三方好友。不能把 ReplyTargetAiAccount 或 OtherAiAccount 的经历、职业、物品、观点改写成 CurrentSpeaker 的第一人称事实。");
        builder.AppendLine("如果目标消息包含“那个人”“他/她”“这件事”等指代，必须结合带事实归属的历史消息解析指向。能确定时返回 referenceStatus=Resolved，并在 factOwnership 中写清相关事实属于谁；不能可靠确定时返回 Ambiguous，并用一个自然短问句澄清，不能猜测。");
        builder.AppendLine("跨时间续聊可以自然接回旧话题，但要意识到时间已经过去；不要假装上一条消息刚刚发生，也不要机械重复问候。");
        builder.AppendLine("newContribution 必须指出本轮相对最近消息新增的内容，不能只是换一种说法赞同上一句。");
        builder.AppendLine("forbiddenClaims 必须包含当前好友没有资料或本人历史依据、因此不能声称亲历的事实类型。");
        builder.AppendLine("referencedSelfMemoryIds 只能填写本轮确实需要使用的本人个人记忆 Id；没有使用时返回空数组。");
        builder.AppendLine("selfMemoryProposals 只记录本轮最终台词确实准备表达、且以后仍需保持一致的动态个人事实，最多两项。普通寒暄、即时情绪和对其他人的事实不得记录。");
        builder.AppendLine("新增稳定身份事实不属于本轮权限；只可建议 OngoingActivity、Plan、Experience 或 Preference。Add 的 targetMemoryId 为 null；Update 和 Archive 必须指向上方当前账号的有效导演记忆。");
        builder.AppendLine("messageCount 必须在允许范围内；简单反应通常一条，普通回答可用一到两条，包含多个关联信息时可用两到三条，确实需要分层说明时可用三到四条。不能为了凑数切碎同一句话，也不要习惯性总选两条。");
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
            "questionMode 只能是 None、Optional、Natural，由你根据本轮动作和自然交流需要决定。",
            "如果规则基线动作为 Answer 或 Close，必须保持该动作。",
            "唯一例外：目标消息的关键指代确实无法从上下文可靠确定时，referenceStatus 必须为 Ambiguous，此时可将 Answer 改为 Ask 进行一次简短澄清。",
            "targetMessageId 必须原样返回；没有目标消息时返回空字符串。",
            "topicFocus 和 responseGoal 应具体、简短，不得包含最终聊天台词。",
            "coveredPoints 列出最近已经说清楚的观点；unresolvedGoals 列出原始要求中仍待完成的部分。",
            "newContribution 指定本轮必须新增的观点、决定、信息或情绪变化，不能只要求附和或复述。",
            "avoidedTopics 列出不要恢复的旧话题；forbiddenClaims 列出没有身份资料或本人历史依据、不可虚构的第一人称事实。",
            "referenceStatus 只能是 None、Resolved、Ambiguous。没有需解析指代时使用 None；能确定指代时使用 Resolved；无法可靠确定时使用 Ambiguous。",
            "referenceResolution 用一句话说明指代对象或歧义；factOwnership 明确列出本轮相关事实分别属于 CurrentSpeaker、ReplyTargetAiAccount、OtherAiAccount 或 LocalUser。回应对象和第三方事实绝不能转写为当前发言者的特点或经历。",
            "referencedSelfMemoryIds 只列出本轮实际用于规划的当前发言者个人记忆 Id。",
            "selfMemoryProposals 最多两项，每项包含 operation、targetMemoryId、type、summary、reason。只建议最终台词会自然表达、以后仍需保持一致的本人动态事实；不能记录他人的经历、普通寒暄或瞬时情绪。",
            "个人记忆 operation 只能是 Add、Update、Archive；类型只能使用 OngoingActivity、Plan、Experience、Preference。Add 的 targetMemoryId 返回 null，Update 和 Archive 必须引用提供的有效导演记忆。",
            "长期记忆只代表当前发言者过去对当前对象形成的认识。只有与本轮目标自然相关时才能用于规划，不能让记忆取代目标消息，也不能把对方经历归到当前发言者名下。",
            "messageCount 表示本轮应独立发送几条聊天消息，必须处于业务层允许范围内。消息数量服务于完整表达和自然节奏，不用于机械切句。",
            "不要固定选择两条：先根据内容判断一条是否足以说完整，再在同样自然的多个数量之间保留变化。",
            "各数组没有内容时返回空数组；普通语义数组最多五项，个人记忆建议最多两项。",
            "严格输出 json 对象，不要输出 Markdown 或额外解释。",
            "json 示例：{\"action\":\"Answer\",\"questionMode\":\"Optional\",\"beat\":\"Clarify\",\"topicFocus\":\"对方询问的时间\",\"responseGoal\":\"给出明确时间\",\"messageCount\":2,\"targetMessageId\":\"00000000-0000-0000-0000-000000000000\",\"coveredPoints\":[],\"unresolvedGoals\":[\"回答具体时间\"],\"newContribution\":\"给出尚未出现的具体时间\",\"avoidedTopics\":[],\"forbiddenClaims\":[\"没有本人历史依据的昨晚行程\"],\"referenceStatus\":\"None\",\"referenceResolution\":\"\",\"factOwnership\":[],\"referencedSelfMemoryIds\":[],\"selfMemoryProposals\":[]}");

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
            $"[{contextMessage.Ownership}] [{FormatSentAt(message.SentAt)}] "
            + $"{message.SenderDisplayName}：{Truncate(message.Content, 400)}");
    }

    private static string FormatSentAt(DateTime sentAt) =>
        sentAt == default ? "时间未知" : sentAt.ToString("yyyy-MM-dd HH:mm");

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

    private static void AppendSelfMemories(
        StringBuilder builder,
        IReadOnlyList<AiConversationSelfMemory> memories)
    {
        builder.AppendLine("当前发言者自己的有效个人记忆：");
        if (memories.Count == 0)
        {
            builder.AppendLine("（暂无）");
            return;
        }

        foreach (AiConversationSelfMemory memory in memories)
        {
            builder.AppendLine(
                $"[{memory.Id}] [{memory.Type}] {Truncate(memory.Summary, 300)}"
                + $"（来源：{memory.Source}，用户锁定：{memory.IsUserLocked}）");
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

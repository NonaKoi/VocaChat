namespace VocaChat.Services;

/// <summary>
/// 在模型导演不可用时使用现有规则生成安全、可执行的导演计划。
/// </summary>
public sealed class RuleBasedConversationDirector : IConversationDirector
{
    private readonly ConversationActionPlanner _actionPlanner;

    public RuleBasedConversationDirector(ConversationActionPlanner actionPlanner)
    {
        _actionPlanner = actionPlanner
            ?? throw new ArgumentNullException(nameof(actionPlanner));
    }

    public Task<ConversationDirectionPlan> CreatePlanAsync(
        AiMessageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(CreateDirectionPlan(
            request,
            _actionPlanner.CreatePlan(request)));
    }

    internal static ConversationDirectionPlan CreateDirectionPlan(
        AiMessageGenerationRequest request,
        ConversationActionPlan actionPlan)
    {
        IReadOnlyList<string> coveredPoints = request.RecentMessages
            .TakeLast(3)
            .Select(message => Truncate(message.Content, 80))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        IReadOnlyList<string> unresolvedGoals = GetUnresolvedGoals(request);

        return new ConversationDirectionPlan(
            actionPlan,
            GetBeat(request, actionPlan.Action),
            GetTopicFocus(request),
            GetResponseGoal(actionPlan.Action),
            request.ReplyTarget?.Message?.MessageId ?? Guid.Empty,
            coveredPoints,
            unresolvedGoals,
            GetNewContribution(actionPlan.Action),
            Array.Empty<string>(),
            new[] { "没有资料或本人历史依据的第一人称经历" },
            usedRuleFallback: true,
            selectedMessageCount: SelectMessageCount(request, actionPlan));
    }

    /// <summary>
    /// 模型导演不可用时，根据当前用户要求选择一个保守且足够表达的消息数量。
    /// </summary>
    internal static int SelectMessageCount(
        AiMessageGenerationRequest request,
        ConversationActionPlan actionPlan)
    {
        AiMessageCountRange? range = request.AllowedMessageCountRange;
        if (range is null)
        {
            return request.ExpectedMessageCount;
        }

        if (range.Minimum == range.Maximum)
        {
            return range.Minimum;
        }

        string targetContent = request.ReplyTarget?.Message?.Content
            ?? request.FocusContent;
        bool explicitlyRequestsMultipleMessages = ContainsAny(
            targetContent,
            "多说几句", "多说一点", "分几条", "分开说", "别只回一句");
        bool needsExpansion = actionPlan.Action == ConversationAction.Answer
            && (actionPlan.MessageLength == ConversationMessageLength.Moderate
                || ContainsAny(targetContent, "具体", "详细", "讲讲", "解释"));
        int preferredCount = explicitlyRequestsMultipleMessages
            || needsExpansion
                ? 2
                : 1;
        return Math.Clamp(preferredCount, range.Minimum, range.Maximum);
    }

    internal static string GetTopicFocus(AiMessageGenerationRequest request)
    {
        string value = request.ReplyTarget?.Message?.Content
            ?? (!string.IsNullOrWhiteSpace(request.Topic)
                ? request.Topic
                : request.FocusContent);
        string normalized = string.IsNullOrWhiteSpace(value)
            ? "当前对话"
            : value.Trim();
        return normalized.Length <= 80
            ? normalized
            : $"{normalized[..79]}…";
    }

    internal static string GetResponseGoal(ConversationAction action) =>
        action switch
        {
            ConversationAction.Acknowledge => "接住对方刚才表达的重点",
            ConversationAction.Answer => "直接回答目标消息中的问题或请求",
            ConversationAction.Ask => "追问一个与当前内容直接相关的小点",
            ConversationAction.Share => "分享一个与当前内容相关的想法或感受",
            ConversationAction.React => "给出符合当前身份的即时反应",
            ConversationAction.Comfort => "让对方感到被理解，不急于给建议",
            ConversationAction.Tease => "在关系允许的范围内自然调侃",
            ConversationAction.Disagree => "表达核心分歧但不展开辩论",
            ConversationAction.Evade => "保持人物一致地含蓄带过",
            ConversationAction.ShiftTopic => "从当前内容自然带到相邻话题",
            ConversationAction.Close => "顺着已有内容自然收住对话",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

    private static ConversationBeat GetBeat(
        AiMessageGenerationRequest request,
        ConversationAction action)
    {
        if (action == ConversationAction.Close
            || request.Scenario ==
                AiMessageGenerationScenario.AutonomousPrivateChatClosing)
        {
            return ConversationBeat.Close;
        }

        if (request.ReplyTarget?.Kind ==
                AiDialogueReplyTargetKind.TopicOpening)
        {
            return ConversationBeat.Introduce;
        }

        if (action == ConversationAction.Disagree)
        {
            return ConversationBeat.Contrast;
        }

        if (action is ConversationAction.Answer or ConversationAction.Ask)
        {
            return ConversationBeat.Clarify;
        }

        return request.RoundNumber is >= 3
            ? ConversationBeat.Resolve
            : ConversationBeat.Develop;
    }

    private static IReadOnlyList<string> GetUnresolvedGoals(
        AiMessageGenerationRequest request)
    {
        string? anchor = request.ConversationAnchor?.Content;
        string? target = request.ReplyTarget?.Message?.Content;
        string? value = !string.IsNullOrWhiteSpace(anchor)
            ? anchor
            : !string.IsNullOrWhiteSpace(target)
                ? target
                : request.Topic;

        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : new[] { Truncate(value.Trim(), 120) };
    }

    private static string GetNewContribution(ConversationAction action) =>
        action switch
        {
            ConversationAction.Answer => "给出尚未出现的直接答案",
            ConversationAction.Ask => "提出一个尚未问过的具体小问题",
            ConversationAction.Disagree => "补充一个与已有观点不同的真实判断",
            ConversationAction.Close => "使用新的简短表达自然收住，不复述结论",
            _ => "增加一个尚未在最近消息中表达的新角度或新反应"
        };

    private static bool ContainsAny(
        string value,
        params string[] candidates) =>
        candidates.Any(candidate => value.Contains(
            candidate,
            StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : $"{value[..maximumLength]}…";
}

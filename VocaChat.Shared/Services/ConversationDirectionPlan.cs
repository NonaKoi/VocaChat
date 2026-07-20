namespace VocaChat.Services;

/// <summary>
/// 表示当前发言在整段会话中的推进位置，而不是单句的表达动作。
/// </summary>
public enum ConversationBeat
{
    Introduce,
    Develop,
    Contrast,
    Clarify,
    Resolve,
    Close
}

/// <summary>
/// 保存导演结合整段会话为一次生成确定的语义目标；不包含最终可见的聊天文本。
/// </summary>
public sealed record ConversationDirectionPlan
{
    public ConversationActionPlan ActionPlan { get; }
    public ConversationBeat Beat { get; }
    public string TopicFocus { get; }
    public string ResponseGoal { get; }
    public Guid TargetMessageId { get; }
    public IReadOnlyList<string> CoveredPoints { get; }
    public IReadOnlyList<string> UnresolvedGoals { get; }
    public string NewContribution { get; }
    public IReadOnlyList<string> AvoidedTopics { get; }
    public IReadOnlyList<string> ForbiddenClaims { get; }
    public bool UsedRuleFallback { get; }
    public int SelectedMessageCount { get; }
    public IReadOnlyList<Guid> ReferencedSelfMemoryIds { get; }
    public IReadOnlyList<AiSelfMemoryProposal> SelfMemoryProposals { get; }

    public ConversationDirectionPlan(
        ConversationActionPlan actionPlan,
        ConversationBeat beat,
        string topicFocus,
        string responseGoal,
        Guid targetMessageId,
        IReadOnlyList<string> coveredPoints,
        IReadOnlyList<string> unresolvedGoals,
        string newContribution,
        IReadOnlyList<string> avoidedTopics,
        IReadOnlyList<string> forbiddenClaims,
        bool usedRuleFallback,
        int selectedMessageCount = 1,
        IReadOnlyList<Guid>? referencedSelfMemoryIds = null,
        IReadOnlyList<AiSelfMemoryProposal>? selfMemoryProposals = null)
    {
        if (selectedMessageCount is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedMessageCount),
                "导演选择的消息数量必须在 0 到 3 之间。");
        }

        ActionPlan = actionPlan
            ?? throw new ArgumentNullException(nameof(actionPlan));
        Beat = beat;
        TopicFocus = topicFocus;
        ResponseGoal = responseGoal;
        TargetMessageId = targetMessageId;
        CoveredPoints = coveredPoints
            ?? throw new ArgumentNullException(nameof(coveredPoints));
        UnresolvedGoals = unresolvedGoals
            ?? throw new ArgumentNullException(nameof(unresolvedGoals));
        NewContribution = newContribution;
        AvoidedTopics = avoidedTopics
            ?? throw new ArgumentNullException(nameof(avoidedTopics));
        ForbiddenClaims = forbiddenClaims
            ?? throw new ArgumentNullException(nameof(forbiddenClaims));
        UsedRuleFallback = usedRuleFallback;
        SelectedMessageCount = selectedMessageCount;
        ReferencedSelfMemoryIds = referencedSelfMemoryIds
            ?? Array.Empty<Guid>();
        SelfMemoryProposals = selfMemoryProposals
            ?? Array.Empty<AiSelfMemoryProposal>();
    }

    /// <summary>
    /// 保留早期调用点的简洁构造方式；新增的会话级约束使用安全默认值。
    /// </summary>
    public ConversationDirectionPlan(
        ConversationActionPlan actionPlan,
        string topicFocus,
        string responseGoal,
        Guid targetMessageId,
        IReadOnlyList<string> avoidedTopics,
        bool UsedRuleFallback)
        : this(
            actionPlan,
            ConversationBeat.Develop,
            topicFocus,
            responseGoal,
            targetMessageId,
            Array.Empty<string>(),
            Array.Empty<string>(),
            responseGoal,
            avoidedTopics,
            new[] { "没有资料或本人历史依据的第一人称经历" },
            UsedRuleFallback)
    {
    }

    /// <summary>
    /// 使用业务层预验证后的建议替换导演原始建议，同时保留其余语义计划。
    /// </summary>
    public ConversationDirectionPlan WithValidatedSelfMemoryPlan(
        IReadOnlyList<Guid> referencedSelfMemoryIds,
        IReadOnlyList<AiSelfMemoryProposal> selfMemoryProposals)
    {
        return new ConversationDirectionPlan(
            ActionPlan,
            Beat,
            TopicFocus,
            ResponseGoal,
            TargetMessageId,
            CoveredPoints,
            UnresolvedGoals,
            NewContribution,
            AvoidedTopics,
            ForbiddenClaims,
            UsedRuleFallback,
            SelectedMessageCount,
            referencedSelfMemoryIds,
            selfMemoryProposals);
    }
}

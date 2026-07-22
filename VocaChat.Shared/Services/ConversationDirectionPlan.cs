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
/// 表示导演对当前消息中代词、省略主语或旧话题指代的判断结果。
/// </summary>
public enum ConversationReferenceStatus
{
    None,
    Resolved,
    Ambiguous
}

/// <summary>
/// 保存本轮需要遵守的指代解析和事实归属约束，避免把第三方事实转移给当前发言者。
/// </summary>
public sealed record ConversationReferencePlan
{
    public static ConversationReferencePlan None { get; } = new(
        ConversationReferenceStatus.None,
        string.Empty,
        Array.Empty<string>());

    public ConversationReferenceStatus Status { get; }
    public string ResolutionSummary { get; }
    public IReadOnlyList<string> FactOwnershipConstraints { get; }

    public ConversationReferencePlan(
        ConversationReferenceStatus status,
        string resolutionSummary,
        IReadOnlyList<string> factOwnershipConstraints)
    {
        Status = status;
        ResolutionSummary = resolutionSummary?.Trim() ?? string.Empty;
        FactOwnershipConstraints = factOwnershipConstraints
            ?? throw new ArgumentNullException(nameof(factOwnershipConstraints));
    }
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
    public ConversationReferencePlan ReferencePlan { get; }

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
        IReadOnlyList<AiSelfMemoryProposal>? selfMemoryProposals = null,
        ConversationReferencePlan? referencePlan = null)
    {
        if (selectedMessageCount is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedMessageCount),
                "导演选择的消息数量必须在 0 到 4 之间。");
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
        ReferencePlan = referencePlan ?? ConversationReferencePlan.None;
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
            selfMemoryProposals,
            ReferencePlan);
    }
}

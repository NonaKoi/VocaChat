using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示群聊中一位 AI 本轮主要面向的对象。
/// </summary>
public enum GroupConversationAudience
{
    LocalUser,
    SpecificAiAccount,
    WholeGroup
}

/// <summary>
/// 表示一位群成员在当前整轮回复中承担的唯一主要职责。
/// </summary>
public enum GroupConversationRole
{
    DirectAnswer,
    Complement,
    AgreeAndExtend,
    Disagree,
    React,
    Comfort,
    Clarify,
    Tease,
    ShiftTopic,
    Close
}

/// <summary>
/// 区分群级导演当前处理的是用户消息，还是好友自主群聊的开场、推进或收束。
/// </summary>
public enum GroupConversationPlanningScenario
{
    UserMessage,
    AutonomousOpening,
    AutonomousContinuation,
    AutonomousClosing
}

/// <summary>
/// 保存群级导演为一位实际发言者确定的回复目标和新增内容。
/// </summary>
public sealed record GroupConversationSpeakerPlan
{
    public required Guid SpeakerAiAccountId { get; init; }
    public Guid? ReplyTargetMessageId { get; init; }
    public Guid? TargetAiAccountId { get; init; }
    public required GroupConversationAudience Audience { get; init; }
    public required GroupConversationRole Role { get; init; }
    public required string ResponseGoal { get; init; }
    public required string NewContribution { get; init; }
    public IReadOnlyList<string> AvoidedRepetition { get; init; } =
        Array.Empty<string>();
}

/// <summary>
/// 保存一次群聊交互的共享语义计划；不包含最终聊天台词。
/// </summary>
public sealed record GroupConversationTurnPlan
{
    public Guid? AnchorMessageId { get; init; }
    public required string TopicFocus { get; init; }
    public required string TurnGoal { get; init; }
    public IReadOnlyList<string> CoveredPoints { get; init; } =
        Array.Empty<string>();
    public IReadOnlyList<string> UnresolvedGoals { get; init; } =
        Array.Empty<string>();
    public required IReadOnlyList<GroupConversationSpeakerPlan> Speakers
        { get; init; }
    public required AiSpeakerSelectionStatus SelectionStatus { get; init; }
    public bool UsedRuleFallback { get; init; }
}

/// <summary>
/// 提供群级导演所需的群成员、锚点消息、最近历史和业务数量边界。
/// </summary>
public sealed record GroupConversationPlanningRequest
{
    public const int DefaultMaximumSpeakerCount = 3;
    public const int DefaultMaximumTotalMessageCount = 12;

    public required GroupChat GroupChat { get; init; }
    public GroupConversationPlanningScenario Scenario { get; init; } =
        GroupConversationPlanningScenario.UserMessage;
    public GroupMessage? AnchorMessage { get; init; }
    public string Topic { get; init; } = string.Empty;
    public Guid? RequiredSpeakerAiAccountId { get; init; }
    public IReadOnlyList<Guid> PreferredSpeakerAiAccountIds { get; init; } =
        Array.Empty<Guid>();
    public IReadOnlyList<GroupMessage> RecentMessages { get; init; } =
        Array.Empty<GroupMessage>();
    public int MaximumSpeakerCount { get; init; } =
        DefaultMaximumSpeakerCount;
    public int MaximumTotalMessageCount { get; init; } =
        DefaultMaximumTotalMessageCount;
}

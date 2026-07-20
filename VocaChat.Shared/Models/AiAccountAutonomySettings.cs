using System;

namespace VocaChat.Models;

/// <summary>
/// 保存一个 AI 账号参与好友自主互动时使用的专有设置。
/// </summary>
public class AiAccountAutonomySettings
{
    public Guid AiAccountId { get; private set; }
    public bool IsEnabled { get; private set; }
    public AutonomousInteractionInitiativeLevel InitiativeLevel { get; private set; }
    public bool CanInitiatePrivateChats { get; private set; }
    public bool CanInitiateGroupChats { get; private set; }
    public bool CanJoinGroupChats { get; private set; }
    public bool UseGlobalReplyDelay { get; private set; }
    public AiReplyDelayMode ReplyDelayMode { get; private set; }
    public long FixedReplyDelayMilliseconds { get; private set; }
    public long MinimumReplyDelayMilliseconds { get; private set; }
    public long MaximumReplyDelayMilliseconds { get; private set; }
    public bool UseGlobalConsecutiveMessageDelay { get; private set; }
    public AiReplyDelayMode ConsecutiveMessageDelayMode { get; private set; }
    public long FixedConsecutiveMessageDelayMilliseconds { get; private set; }
    public long MinimumConsecutiveMessageDelayMilliseconds { get; private set; }
    public long MaximumConsecutiveMessageDelayMilliseconds { get; private set; }
    public bool UseGlobalQuestionPolicy { get; private set; }
    public int MaximumConsecutiveQuestionTurns { get; private set; }

    /// <summary>
    /// 供 EF Core 从数据库还原设置使用。
    /// </summary>
    private AiAccountAutonomySettings()
    {
    }

    /// <summary>
    /// 为一个已有 AI 账号创建安全的默认专有设置。
    /// </summary>
    internal AiAccountAutonomySettings(Guid aiAccountId)
    {
        AiAccountId = aiAccountId;
        IsEnabled = true;
        InitiativeLevel = AutonomousInteractionInitiativeLevel.Normal;
        CanInitiatePrivateChats = true;
        CanInitiateGroupChats = true;
        CanJoinGroupChats = true;
        UseGlobalReplyDelay = true;
        ReplyDelayMode = AutonomousInteractionSettings.DefaultReplyDelayMode;
        FixedReplyDelayMilliseconds = AutonomousInteractionSettings
            .DefaultFixedReplyDelayMilliseconds;
        MinimumReplyDelayMilliseconds = AutonomousInteractionSettings
            .DefaultMinimumReplyDelayMilliseconds;
        MaximumReplyDelayMilliseconds = AutonomousInteractionSettings
            .DefaultMaximumReplyDelayMilliseconds;
        UseGlobalConsecutiveMessageDelay = true;
        ConsecutiveMessageDelayMode = AutonomousInteractionSettings
            .DefaultConsecutiveMessageDelayMode;
        FixedConsecutiveMessageDelayMilliseconds = AutonomousInteractionSettings
            .DefaultFixedConsecutiveMessageDelayMilliseconds;
        MinimumConsecutiveMessageDelayMilliseconds = AutonomousInteractionSettings
            .DefaultMinimumConsecutiveMessageDelayMilliseconds;
        MaximumConsecutiveMessageDelayMilliseconds = AutonomousInteractionSettings
            .DefaultMaximumConsecutiveMessageDelayMilliseconds;
        UseGlobalQuestionPolicy = true;
        MaximumConsecutiveQuestionTurns = AutonomousInteractionSettings
            .DefaultMaximumConsecutiveQuestionTurns;
    }

    /// <summary>
    /// 保存已经由 Service 验证通过的专有设置。
    /// </summary>
    internal void Update(
        bool isEnabled,
        AutonomousInteractionInitiativeLevel initiativeLevel,
        bool canInitiatePrivateChats,
        bool canInitiateGroupChats,
        bool canJoinGroupChats,
        bool useGlobalReplyDelay,
        AiReplyDelayMode replyDelayMode,
        long fixedReplyDelayMilliseconds,
        long minimumReplyDelayMilliseconds,
        long maximumReplyDelayMilliseconds,
        bool useGlobalConsecutiveMessageDelay,
        AiReplyDelayMode consecutiveMessageDelayMode,
        long fixedConsecutiveMessageDelayMilliseconds,
        long minimumConsecutiveMessageDelayMilliseconds,
        long maximumConsecutiveMessageDelayMilliseconds,
        bool useGlobalQuestionPolicy,
        int maximumConsecutiveQuestionTurns)
    {
        IsEnabled = isEnabled;
        InitiativeLevel = initiativeLevel;
        CanInitiatePrivateChats = canInitiatePrivateChats;
        CanInitiateGroupChats = canInitiateGroupChats;
        CanJoinGroupChats = canJoinGroupChats;
        UseGlobalReplyDelay = useGlobalReplyDelay;
        ReplyDelayMode = replyDelayMode;
        FixedReplyDelayMilliseconds = fixedReplyDelayMilliseconds;
        MinimumReplyDelayMilliseconds = minimumReplyDelayMilliseconds;
        MaximumReplyDelayMilliseconds = maximumReplyDelayMilliseconds;
        UseGlobalConsecutiveMessageDelay = useGlobalConsecutiveMessageDelay;
        ConsecutiveMessageDelayMode = consecutiveMessageDelayMode;
        FixedConsecutiveMessageDelayMilliseconds =
            fixedConsecutiveMessageDelayMilliseconds;
        MinimumConsecutiveMessageDelayMilliseconds =
            minimumConsecutiveMessageDelayMilliseconds;
        MaximumConsecutiveMessageDelayMilliseconds =
            maximumConsecutiveMessageDelayMilliseconds;
        UseGlobalQuestionPolicy = useGlobalQuestionPolicy;
        MaximumConsecutiveQuestionTurns = maximumConsecutiveQuestionTurns;
    }
}

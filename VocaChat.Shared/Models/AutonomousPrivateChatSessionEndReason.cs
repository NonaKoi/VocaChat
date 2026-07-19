namespace VocaChat.Models;

/// <summary>
/// 说明一次好友自主私信为何结束，供诊断和后续规则使用。
/// </summary>
public enum AutonomousPrivateChatSessionEndReason
{
    NaturalConclusion,
    PlannedLimitReached,
    HardLimitReached,
    ParticipantUnavailable,
    InteractionDisabled,
    GenerationFailed,
    MessagePersistenceFailed,
    RelationshipUpdateFailed,
    CancelledByUser,
    ContinuationProbabilityDeclined
}

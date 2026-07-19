namespace VocaChat.Services;

/// <summary>
/// 区分一次自主好友群聊执行停止或失败的阶段。
/// </summary>
public enum AutonomousGroupChatExecutionStatus
{
    Completed,
    DecisionRejected,
    PlanningFailed,
    GroupChatCreationFailed,
    SessionCreationFailed,
    ParticipantUnavailable,
    GenerationFailed,
    MessagePersistenceFailed,
    SessionFinalizationFailed
}

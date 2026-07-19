namespace VocaChat.Models;

/// <summary>
/// 记录一次自主好友群聊结束或失败的明确原因。
/// </summary>
public enum AutonomousGroupChatSessionEndReason
{
    Completed,
    ParticipantUnavailable,
    GenerationFailed,
    MessagePersistenceFailed
}

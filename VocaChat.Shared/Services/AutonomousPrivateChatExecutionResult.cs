using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示一次受控自主私信执行最终停在了哪个阶段。
/// </summary>
public enum AutonomousPrivateChatExecutionStatus
{
    Completed,
    DecisionRejected,
    ChatCreationFailed,
    MessagePersistenceFailed,
    RelationshipRecordFailed
}

/// <summary>
/// 返回判断、会话和消息结果，使调用方能够区分未执行与部分成功。
/// </summary>
public sealed class AutonomousPrivateChatExecutionResult
{
    public AutonomousPrivateChatExecutionStatus Status { get; init; }
    public AutonomousPrivateChatDecision Decision { get; init; } = new();
    public PrivateChat? PrivateChat { get; init; }
    public bool PrivateChatCreated { get; init; }
    public PrivateMessage? InitiatorMessage { get; init; }
    public PrivateMessage? RecipientReply { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}

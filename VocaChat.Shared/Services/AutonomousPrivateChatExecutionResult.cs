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
    SessionCreationFailed,
    PlanningFailed,
    GenerationFailed,
    MessagePersistenceFailed,
    RelationshipRecordFailed,
    SessionFinalizationFailed
}

/// <summary>
/// 返回判断、会话和消息结果，使调用方能够区分未执行与部分成功。
/// </summary>
public sealed class AutonomousPrivateChatExecutionResult
{
    public AutonomousPrivateChatExecutionStatus Status { get; init; }
    public AutonomousPrivateChatDecision Decision { get; init; } = new();
    public PrivateChat? PrivateChat { get; init; }
    public AutonomousPrivateChatSession? Session { get; init; }
    public bool PrivateChatCreated { get; init; }
    public IReadOnlyList<AutonomousPrivateChatRound> Rounds { get; init; } =
        Array.Empty<AutonomousPrivateChatRound>();
    public IReadOnlyList<PrivateMessage> Messages { get; init; } =
        Array.Empty<PrivateMessage>();
    public string ErrorMessage { get; init; } = string.Empty;
}

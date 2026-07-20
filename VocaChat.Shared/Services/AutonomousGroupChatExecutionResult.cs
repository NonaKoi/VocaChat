using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 返回自主好友群聊执行结果和已经持久化的正式消息。
/// </summary>
public sealed class AutonomousGroupChatExecutionResult
{
    public AutonomousGroupChatExecutionStatus Status { get; init; }
    public AutonomousGroupChatDecision Decision { get; init; } = null!;
    public GroupChat? GroupChat { get; init; }
    public bool GroupChatCreated { get; init; }
    public AutonomousGroupChatSession? Session { get; init; }
    public IReadOnlyList<AutonomousGroupChatRound> Rounds { get; init; } =
        Array.Empty<AutonomousGroupChatRound>();
    public IReadOnlyList<GroupMessage> Messages { get; init; } =
        Array.Empty<GroupMessage>();
    public string ErrorMessage { get; init; } = string.Empty;
}

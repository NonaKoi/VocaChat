namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>
/// 返回一次持久化的自主好友群聊 Session 摘要。
/// </summary>
public sealed class AutonomousGroupChatSessionResponse
{
    public Guid Id { get; init; }
    public Guid GroupChatId { get; init; }
    public Guid InitiatorAiAccountId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public IReadOnlyList<Guid> ParticipantAiAccountIds { get; init; } =
        Array.Empty<Guid>();
    public string Status { get; init; } = string.Empty;
    public string? EndReason { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime LastActivityAt { get; init; }
    public DateTime? EndedAt { get; init; }
}

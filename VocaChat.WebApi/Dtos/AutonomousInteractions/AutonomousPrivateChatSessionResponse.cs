namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>
/// 表示一次可查询的好友自主私信生命周期摘要。
/// </summary>
public sealed class AutonomousPrivateChatSessionResponse
{
    public Guid Id { get; init; }
    public Guid PrivateChatId { get; init; }
    public Guid InitiatorAiAccountId { get; init; }
    public Guid RecipientAiAccountId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public int MaximumRounds { get; init; }
    public int ContinuationRatePercent { get; init; }
    public int CompletedRounds { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? EndReason { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime LastActivityAt { get; init; }
    public DateTime? EndedAt { get; init; }
}

namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>表示一次自主私信普通轮或收束轮的可解释执行摘要。</summary>
public sealed class AutonomousPrivateChatRoundResponse
{
    public Guid Id { get; init; }
    public int RoundNumber { get; init; }
    public bool IsClosing { get; init; }
    public double? OccurrenceProbability { get; init; }
    public double? RandomRoll { get; init; }
    public string InitiatorMessageMode { get; init; } = string.Empty;
    public string RecipientMessageMode { get; init; } = string.Empty;
    public int InitiatorMessageCount { get; init; }
    public int RecipientMessageCount { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

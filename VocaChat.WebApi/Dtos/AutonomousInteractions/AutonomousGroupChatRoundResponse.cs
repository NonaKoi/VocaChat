namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>
/// 返回一次自主好友群聊普通轮或最终收束轮的持久化摘要。
/// </summary>
public sealed class AutonomousGroupChatRoundResponse
{
    public Guid Id { get; init; }
    public int RoundNumber { get; init; }
    public bool IsClosing { get; init; }
    public double? OccurrenceProbability { get; init; }
    public double? RandomRoll { get; init; }
    public int PlannedSpeakerCount { get; init; }
    public int PlannedMessageCount { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

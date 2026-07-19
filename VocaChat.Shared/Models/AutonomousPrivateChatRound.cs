namespace VocaChat.Models;

/// <summary>
/// 保存一次自主私信中的普通轮或最终收束轮，以及当时采用的概率和发言形式。
/// </summary>
public class AutonomousPrivateChatRound
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public int RoundNumber { get; private set; }
    public bool IsClosing { get; private set; }
    public double? OccurrenceProbability { get; private set; }
    public double? RandomRoll { get; private set; }
    public AutonomousPrivateChatMessageMode InitiatorMessageMode { get; private set; }
    public AutonomousPrivateChatMessageMode RecipientMessageMode { get; private set; }
    public int InitiatorMessageCount { get; private set; }
    public int RecipientMessageCount { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private AutonomousPrivateChatRound()
    {
    }

    internal AutonomousPrivateChatRound(
        Guid sessionId,
        int roundNumber,
        bool isClosing,
        double? occurrenceProbability,
        double? randomRoll,
        AutonomousPrivateChatMessageMode initiatorMessageMode,
        AutonomousPrivateChatMessageMode recipientMessageMode,
        int initiatorMessageCount,
        int recipientMessageCount,
        DateTime startedAt)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        RoundNumber = roundNumber;
        IsClosing = isClosing;
        OccurrenceProbability = occurrenceProbability;
        RandomRoll = randomRoll;
        InitiatorMessageMode = initiatorMessageMode;
        RecipientMessageMode = recipientMessageMode;
        InitiatorMessageCount = initiatorMessageCount;
        RecipientMessageCount = recipientMessageCount;
        StartedAt = startedAt;
    }

    internal void Complete(DateTime completedAt)
    {
        if (CompletedAt is not null)
        {
            throw new InvalidOperationException("自主私信轮次已经完成。");
        }

        CompletedAt = completedAt;
    }
}

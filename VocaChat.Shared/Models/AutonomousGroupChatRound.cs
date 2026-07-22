namespace VocaChat.Models;

/// <summary>
/// 保存一次自主好友群聊的普通轮或最终收束轮及其概率与计划规模。
/// </summary>
public class AutonomousGroupChatRound
{
    internal const int MaximumPlannedSpeakerCount = 3;
    internal const int MaximumPlannedMessageCount = 12;

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public int RoundNumber { get; private set; }
    public bool IsClosing { get; private set; }
    public double? OccurrenceProbability { get; private set; }
    public double? RandomRoll { get; private set; }
    public int PlannedSpeakerCount { get; private set; }
    public int PlannedMessageCount { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private AutonomousGroupChatRound()
    {
    }

    internal AutonomousGroupChatRound(
        Guid sessionId,
        int roundNumber,
        bool isClosing,
        double? occurrenceProbability,
        double? randomRoll,
        int plannedSpeakerCount,
        int plannedMessageCount,
        DateTime startedAt)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        RoundNumber = roundNumber;
        IsClosing = isClosing;
        OccurrenceProbability = occurrenceProbability;
        RandomRoll = randomRoll;
        PlannedSpeakerCount = plannedSpeakerCount;
        PlannedMessageCount = plannedMessageCount;
        StartedAt = startedAt;
    }

    internal void Complete(DateTime completedAt)
    {
        if (CompletedAt is not null)
        {
            throw new InvalidOperationException(
                "自主好友群聊轮次已经完成。");
        }

        CompletedAt = completedAt;
    }

    internal void UpdatePlannedMessageCount(int plannedMessageCount)
    {
        if (CompletedAt is not null)
        {
            throw new InvalidOperationException(
                "已经完成的自主好友群聊轮次不能再调整计划消息数。");
        }

        if (plannedMessageCount < PlannedSpeakerCount
            || plannedMessageCount > MaximumPlannedMessageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plannedMessageCount),
                $"计划消息数必须在发言者人数与 {MaximumPlannedMessageCount} 之间。");
        }

        PlannedMessageCount = plannedMessageCount;
    }
}

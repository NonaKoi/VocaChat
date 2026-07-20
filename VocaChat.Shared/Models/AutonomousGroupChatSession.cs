namespace VocaChat.Models;

/// <summary>
/// 表示一个好友群聊中有限、可追踪的一次自主交流片段。
/// </summary>
public class AutonomousGroupChatSession
{
    internal const int TopicMaxLength = 200;

    private readonly List<AiAccount> _participants = new();

    public Guid Id { get; private set; }
    public Guid GroupChatId { get; private set; }
    public Guid InitiatorAiAccountId { get; private set; }
    public string Topic { get; private set; }
    public IReadOnlyList<AiAccount> Participants => _participants.AsReadOnly();
    public int MaximumRounds { get; private set; }
    public int ContinuationRatePercent { get; private set; }
    public int CompletedRounds { get; private set; }
    public AutonomousGroupChatSessionStatus Status { get; private set; }
    public AutonomousGroupChatSessionEndReason? EndReason { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime LastActivityAt { get; private set; }
    public DateTime? EndedAt { get; private set; }

    private AutonomousGroupChatSession()
    {
        Topic = string.Empty;
    }

    internal AutonomousGroupChatSession(
        Guid groupChatId,
        Guid initiatorAiAccountId,
        string topic,
        IEnumerable<AiAccount> participants,
        int maximumRounds,
        int continuationRatePercent,
        DateTime startedAt)
    {
        Id = Guid.NewGuid();
        GroupChatId = groupChatId;
        InitiatorAiAccountId = initiatorAiAccountId;
        Topic = topic;
        _participants.AddRange(participants);
        MaximumRounds = maximumRounds;
        ContinuationRatePercent = continuationRatePercent;
        CompletedRounds = 0;
        Status = AutonomousGroupChatSessionStatus.Running;
        StartedAt = startedAt;
        LastActivityAt = startedAt;
    }

    internal void RecordMessageActivity(DateTime occurredAt)
    {
        EnsureRunning();
        LastActivityAt = occurredAt;
    }

    internal void RecordCompletedRound(DateTime occurredAt)
    {
        EnsureRunning();

        if (CompletedRounds >= MaximumRounds)
        {
            throw new InvalidOperationException(
                "自主好友群聊已经达到最大轮数。");
        }

        CompletedRounds++;
        LastActivityAt = occurredAt;
    }

    internal void Complete(
        AutonomousGroupChatSessionEndReason endReason,
        DateTime endedAt)
    {
        if (endReason is not (
                AutonomousGroupChatSessionEndReason.Completed
                or AutonomousGroupChatSessionEndReason.NaturalConclusion
                or AutonomousGroupChatSessionEndReason
                    .ContinuationProbabilityDeclined
                or AutonomousGroupChatSessionEndReason.HardLimitReached))
        {
            throw new ArgumentException(
                "完成状态需要使用正常结束原因。",
                nameof(endReason));
        }

        End(
            AutonomousGroupChatSessionStatus.Completed,
            endReason,
            endedAt);
    }

    internal void Fail(
        AutonomousGroupChatSessionEndReason endReason,
        DateTime endedAt)
    {
        if (endReason is AutonomousGroupChatSessionEndReason.Completed
            or AutonomousGroupChatSessionEndReason.NaturalConclusion
            or AutonomousGroupChatSessionEndReason
                .ContinuationProbabilityDeclined
            or AutonomousGroupChatSessionEndReason.HardLimitReached)
        {
            throw new ArgumentException(
                "失败状态不能使用完成原因。",
                nameof(endReason));
        }

        End(AutonomousGroupChatSessionStatus.Failed, endReason, endedAt);
    }

    private void End(
        AutonomousGroupChatSessionStatus status,
        AutonomousGroupChatSessionEndReason endReason,
        DateTime endedAt)
    {
        EnsureRunning();
        Status = status;
        EndReason = endReason;
        EndedAt = endedAt;
        LastActivityAt = endedAt;
    }

    private void EnsureRunning()
    {
        if (Status != AutonomousGroupChatSessionStatus.Running)
        {
            throw new InvalidOperationException(
                "自主好友群聊已经结束，不能继续修改。");
        }
    }
}

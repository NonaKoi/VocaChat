namespace VocaChat.Models;

/// <summary>
/// 表示一个已有好友私信中有限、可追踪的一次自主交流片段。
/// </summary>
public class AutonomousPrivateChatSession
{
    internal const int TopicMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid PrivateChatId { get; private set; }
    public Guid InitiatorAiAccountId { get; private set; }
    public Guid RecipientAiAccountId { get; private set; }
    public string Topic { get; private set; }
    public int MaximumRounds { get; private set; }
    public int ContinuationRatePercent { get; private set; }
    public int CompletedRounds { get; private set; }
    public AutonomousPrivateChatSessionStatus Status { get; private set; }
    public AutonomousPrivateChatSessionEndReason? EndReason { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime LastActivityAt { get; private set; }
    public DateTime? EndedAt { get; private set; }

    private AutonomousPrivateChatSession()
    {
        Topic = string.Empty;
    }

    internal AutonomousPrivateChatSession(
        Guid privateChatId,
        Guid initiatorAiAccountId,
        Guid recipientAiAccountId,
        string topic,
        int maximumRounds,
        int continuationRatePercent,
        DateTime startedAt)
    {
        Id = Guid.NewGuid();
        PrivateChatId = privateChatId;
        InitiatorAiAccountId = initiatorAiAccountId;
        RecipientAiAccountId = recipientAiAccountId;
        Topic = topic;
        MaximumRounds = maximumRounds;
        ContinuationRatePercent = continuationRatePercent;
        CompletedRounds = 0;
        Status = AutonomousPrivateChatSessionStatus.Running;
        StartedAt = startedAt;
        LastActivityAt = startedAt;
    }

    /// <summary>
    /// 在一轮消息已经成功保存时推进轮数和最后活动时间。
    /// </summary>
    internal void RecordCompletedRound(DateTime occurredAt)
    {
        if (Status != AutonomousPrivateChatSessionStatus.Running)
        {
            throw new InvalidOperationException("只有运行中的自主私信可以推进轮数。");
        }

        if (CompletedRounds >= MaximumRounds)
        {
            throw new InvalidOperationException("自主私信已经达到最大轮数。");
        }

        CompletedRounds++;
        LastActivityAt = occurredAt;
    }

    /// <summary>
    /// 在一条消息已经成功保存时更新活动时间，但不提前完成当前轮次。
    /// </summary>
    internal void RecordMessageActivity(DateTime occurredAt)
    {
        if (Status != AutonomousPrivateChatSessionStatus.Running)
        {
            throw new InvalidOperationException("只有运行中的自主私信可以记录消息活动。");
        }

        LastActivityAt = occurredAt;
    }

    /// <summary>
    /// 使用正常结束原因完成当前 Session。
    /// </summary>
    internal void Complete(
        AutonomousPrivateChatSessionEndReason endReason,
        DateTime endedAt)
    {
        if (endReason is not (
                AutonomousPrivateChatSessionEndReason.NaturalConclusion
                or AutonomousPrivateChatSessionEndReason.PlannedLimitReached
                or AutonomousPrivateChatSessionEndReason.HardLimitReached
                or AutonomousPrivateChatSessionEndReason.ContinuationProbabilityDeclined))
        {
            throw new ArgumentException("完成状态需要使用正常结束原因。", nameof(endReason));
        }

        End(AutonomousPrivateChatSessionStatus.Completed, endReason, endedAt);
    }

    /// <summary>
    /// 使用明确失败原因终止当前 Session，并保留此前已经完成的轮次。
    /// </summary>
    internal void Fail(
        AutonomousPrivateChatSessionEndReason endReason,
        DateTime endedAt)
    {
        if (endReason is AutonomousPrivateChatSessionEndReason.NaturalConclusion
            or AutonomousPrivateChatSessionEndReason.PlannedLimitReached
            or AutonomousPrivateChatSessionEndReason.HardLimitReached
            or AutonomousPrivateChatSessionEndReason.ContinuationProbabilityDeclined
            or AutonomousPrivateChatSessionEndReason.CancelledByUser)
        {
            throw new ArgumentException("失败状态需要使用失败结束原因。", nameof(endReason));
        }

        End(AutonomousPrivateChatSessionStatus.Failed, endReason, endedAt);
    }

    private void End(
        AutonomousPrivateChatSessionStatus status,
        AutonomousPrivateChatSessionEndReason endReason,
        DateTime endedAt)
    {
        if (Status != AutonomousPrivateChatSessionStatus.Running)
        {
            throw new InvalidOperationException("自主私信已经结束，不能重复修改终态。");
        }

        Status = status;
        EndReason = endReason;
        EndedAt = endedAt;
        LastActivityAt = endedAt;
    }
}

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
        DateTime startedAt)
    {
        Id = Guid.NewGuid();
        GroupChatId = groupChatId;
        InitiatorAiAccountId = initiatorAiAccountId;
        Topic = topic;
        _participants.AddRange(participants);
        Status = AutonomousGroupChatSessionStatus.Running;
        StartedAt = startedAt;
        LastActivityAt = startedAt;
    }

    internal void RecordMessageActivity(DateTime occurredAt)
    {
        EnsureRunning();
        LastActivityAt = occurredAt;
    }

    internal void Complete(DateTime endedAt)
    {
        End(
            AutonomousGroupChatSessionStatus.Completed,
            AutonomousGroupChatSessionEndReason.Completed,
            endedAt);
    }

    internal void Fail(
        AutonomousGroupChatSessionEndReason endReason,
        DateTime endedAt)
    {
        if (endReason == AutonomousGroupChatSessionEndReason.Completed)
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

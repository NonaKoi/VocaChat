namespace VocaChat.Services;

/// <summary>
/// 冻结一次已获准自主好友群聊的成员、发起者和话题。
/// </summary>
public sealed class AutonomousGroupChatPlan
{
    public const int TopicMaxLength = 200;

    public IReadOnlyList<Guid> MemberAiAccountIds { get; init; } =
        Array.Empty<Guid>();
    public Guid InitiatorAiAccountId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public bool IncludesLocalUser { get; init; }
    public int MaximumRounds { get; init; }
    public int ContinuationRatePercent { get; init; }
    public AutonomousGroupChatDecision Decision { get; init; } = null!;
}

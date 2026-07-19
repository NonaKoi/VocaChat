namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>
/// 返回一次自主好友群聊判断的成员、发起者和可解释评分。
/// </summary>
public sealed class AutonomousGroupChatDecisionResponse
{
    public bool IsApproved { get; init; }
    public string Stage { get; init; } = string.Empty;
    public IReadOnlyList<Guid> ParticipantAiAccountIds { get; init; } =
        Array.Empty<Guid>();
    public Guid? InitiatorAiAccountId { get; init; }
    public int MaximumMembers { get; init; }
    public double AverageRelationshipScore { get; init; }
    public double WeakestRelationshipScore { get; init; }
    public double SharedInterestBonus { get; init; }
    public int InitiativeAdjustment { get; init; }
    public double RandomJitter { get; init; }
    public double FinalScore { get; init; }
    public double Threshold { get; init; }
}

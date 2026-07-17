namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>表示自主私信判断的结果、发起者和评分明细。</summary>
public sealed class AutonomousPrivateChatDecisionResponse
{
    public bool IsApproved { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string InteractionType { get; init; } = "PrivateChat";
    public Guid FirstAiAccountId { get; init; }
    public Guid SecondAiAccountId { get; init; }
    public Guid? InitiatorAiAccountId { get; init; }
    public Guid? RecipientAiAccountId { get; init; }
    public double RelationshipScore { get; init; }
    public int InitiativeAdjustment { get; init; }
    public double RandomJitter { get; init; }
    public double FinalScore { get; init; }
    public double Threshold { get; init; }
    public DateTime? CooldownEndsAt { get; init; }
}

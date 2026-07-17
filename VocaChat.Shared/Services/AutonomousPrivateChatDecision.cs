namespace VocaChat.Services;

/// <summary>
/// 保存一次自主私信判断的结构化结果和可解释评分明细。
/// </summary>
public sealed class AutonomousPrivateChatDecision
{
    public bool IsApproved =>
        Stage == AutonomousPrivateChatDecisionStage.Approved;
    public AutonomousPrivateChatDecisionStage Stage { get; init; }
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

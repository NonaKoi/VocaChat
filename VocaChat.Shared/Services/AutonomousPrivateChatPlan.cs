using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 保存一次自主私信开始时冻结的设置、话题和双向关系快照。
/// </summary>
public sealed class AutonomousPrivateChatPlan
{
    public string Topic { get; init; } = string.Empty;
    public int MaximumRounds { get; init; }
    public int ContinuationRatePercent { get; init; }
    public double InitiatorToRecipientRelationshipScore { get; init; }
    public double RecipientToInitiatorRelationshipScore { get; init; }
    public double MutualRelationshipScore { get; init; }
    public AutonomousInteractionInitiativeLevel InitiatorInitiativeLevel { get; init; }
}

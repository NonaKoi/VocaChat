namespace VocaChat.Models;

/// <summary>
/// 记录一次自主私信 Session 对某一个关系方向实际产生的有界变化。
/// </summary>
public class AiRelationshipChange
{
    internal const int ReasonMaxLength = 500;
    internal const int MinimumFamiliarityDelta = 0;
    internal const int MaximumFamiliarityDelta = 1;
    internal const int MinimumAffinityDelta = -3;
    internal const int MaximumAffinityDelta = 3;
    internal const int MinimumTrustDelta = -2;
    internal const int MaximumTrustDelta = 2;

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid FromAiAccountId { get; private set; }
    public Guid ToAiAccountId { get; private set; }
    public int FamiliarityDelta { get; private set; }
    public int AffinityDelta { get; private set; }
    public int TrustDelta { get; private set; }
    public string Reason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public AutonomousPrivateChatSession Session { get; private set; }
    public AiAccount FromAiAccount { get; private set; }
    public AiAccount ToAiAccount { get; private set; }

    private AiRelationshipChange()
    {
        Reason = string.Empty;
        Session = null!;
        FromAiAccount = null!;
        ToAiAccount = null!;
    }

    internal AiRelationshipChange(
        Guid sessionId,
        Guid fromAiAccountId,
        Guid toAiAccountId,
        int familiarityDelta,
        int affinityDelta,
        int trustDelta,
        string reason,
        DateTime createdAt)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        FromAiAccountId = fromAiAccountId;
        ToAiAccountId = toAiAccountId;
        FamiliarityDelta = familiarityDelta;
        AffinityDelta = affinityDelta;
        TrustDelta = trustDelta;
        Reason = reason;
        CreatedAt = createdAt;
        Session = null!;
        FromAiAccount = null!;
        ToAiAccount = null!;
    }
}

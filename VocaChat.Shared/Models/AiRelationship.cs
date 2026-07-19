using System;

namespace VocaChat.Models;

/// <summary>
/// 表示一个 AI 账号对另一个 AI 账号形成的有方向关系。
/// </summary>
public class AiRelationship
{
    internal const int DefaultFamiliarity = 10;
    internal const int DefaultAffinity = 0;
    internal const int DefaultTrust = 10;

    public Guid FromAiAccountId { get; private set; }
    public Guid ToAiAccountId { get; private set; }
    public int Familiarity { get; private set; }
    public int Affinity { get; private set; }
    public int Trust { get; private set; }
    public int InteractionCount { get; private set; }
    public DateTime? LastInteractionAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public AiAccount FromAiAccount { get; private set; }
    public AiAccount ToAiAccount { get; private set; }

    private AiRelationship()
    {
        FromAiAccount = null!;
        ToAiAccount = null!;
    }

    /// <summary>
    /// 为两个不同的已有账号创建尚未持久化的中性默认关系。
    /// </summary>
    internal AiRelationship(Guid fromAiAccountId, Guid toAiAccountId)
    {
        FromAiAccountId = fromAiAccountId;
        ToAiAccountId = toAiAccountId;
        Familiarity = DefaultFamiliarity;
        Affinity = DefaultAffinity;
        Trust = DefaultTrust;
        FromAiAccount = null!;
        ToAiAccount = null!;
    }

    /// <summary>
    /// 保存已经由 Service 验证通过的关系数值。
    /// </summary>
    internal void Update(int familiarity, int affinity, int trust)
    {
        Familiarity = familiarity;
        Affinity = affinity;
        Trust = trust;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 记录一次实际互动，并保留最后互动时间。
    /// </summary>
    internal void RecordInteraction(DateTime occurredAt)
    {
        InteractionCount++;
        LastInteractionAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    /// <summary>
    /// 应用一次已经由关系演化 Service 限制过的 Session 结果，并返回实际增量。
    /// </summary>
    internal (int FamiliarityDelta, int AffinityDelta, int TrustDelta)
        ApplySessionOutcome(
            int familiarityDelta,
            int affinityDelta,
            int trustDelta,
            DateTime occurredAt)
    {
        int previousFamiliarity = Familiarity;
        int previousAffinity = Affinity;
        int previousTrust = Trust;

        Familiarity = Math.Clamp(Familiarity + familiarityDelta, 0, 100);
        Affinity = Math.Clamp(Affinity + affinityDelta, -100, 100);
        Trust = Math.Clamp(Trust + trustDelta, 0, 100);
        RecordInteraction(occurredAt);

        return (
            Familiarity - previousFamiliarity,
            Affinity - previousAffinity,
            Trust - previousTrust);
    }
}

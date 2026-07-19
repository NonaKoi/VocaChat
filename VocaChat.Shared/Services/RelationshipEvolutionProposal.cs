namespace VocaChat.Services;

/// <summary>
/// 保存业务规则已经从语义信号映射出的一个方向有界变化。
/// </summary>
internal sealed record RelationshipDirectionChange(
    int AffinityDelta,
    int TrustDelta,
    string Reason);

/// <summary>
/// 保存一次 Session 两个固定方向的关系变化建议。
/// </summary>
internal sealed record RelationshipEvolutionProposal(
    RelationshipDirectionChange InitiatorToRecipient,
    RelationshipDirectionChange RecipientToInitiator);

/// <summary>
/// 将模型语义信号映射为业务允许的小幅关系数值，而不是采用模型数值。
/// </summary>
internal static class SessionInsightRelationshipMapper
{
    public static RelationshipEvolutionProposal Map(
        SessionInsightAnalysis analysis,
        IReadOnlyCollection<Guid> validMessageIds)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(validMessageIds);
        return new RelationshipEvolutionProposal(
            MapDirection(analysis.InitiatorPerspective, validMessageIds),
            MapDirection(analysis.RecipientPerspective, validMessageIds));
    }

    private static RelationshipDirectionChange MapDirection(
        DirectionalSessionInsight insight,
        IReadOnlyCollection<Guid> validMessageIds)
    {
        bool hasSemanticSignal =
            insight.AffinityPolarity != RelationshipSignalPolarity.Neutral
            || insight.TrustPolarity != RelationshipSignalPolarity.Neutral;
        bool evidenceIsValid =
            insight.RelationshipEvidenceMessageIds.Count > 0
            && insight.RelationshipEvidenceMessageIds.All(
                validMessageIds.Contains);

        if (hasSemanticSignal && !evidenceIsValid)
        {
            return new RelationshipDirectionChange(
                0,
                0,
                "关系信号缺少有效消息证据，未应用语义变化。");
        }

        int affinityDelta = MapSignal(
            insight.AffinityPolarity,
            insight.AffinityStrength,
            maximumMagnitude: 3);
        int trustDelta = MapSignal(
            insight.TrustPolarity,
            insight.TrustStrength,
            maximumMagnitude: 2);
        return new RelationshipDirectionChange(
            affinityDelta,
            trustDelta,
            insight.Reason);
    }

    private static int MapSignal(
        RelationshipSignalPolarity polarity,
        RelationshipSignalStrength strength,
        int maximumMagnitude)
    {
        if (!Enum.IsDefined(polarity)
            || !Enum.IsDefined(strength)
            || (polarity == RelationshipSignalPolarity.Neutral)
                != (strength == RelationshipSignalStrength.None)
            || polarity == RelationshipSignalPolarity.Neutral)
        {
            return 0;
        }

        int magnitude = strength switch
        {
            RelationshipSignalStrength.Low => 1,
            RelationshipSignalStrength.Medium => Math.Min(2, maximumMagnitude),
            RelationshipSignalStrength.High => maximumMagnitude,
            _ => 0
        };

        return polarity == RelationshipSignalPolarity.Negative
            ? -magnitude
            : magnitude;
    }
}

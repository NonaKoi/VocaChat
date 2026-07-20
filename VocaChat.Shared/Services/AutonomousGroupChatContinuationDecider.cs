namespace VocaChat.Services;

/// <summary>
/// 结合群关系和上一轮参与度计算严格递减的下一轮发生概率。
/// </summary>
public sealed class AutonomousGroupChatContinuationDecider
{
    public AutonomousGroupChatContinuationDecision Decide(
        AutonomousGroupChatPlan plan,
        double previousOccurrenceProbability,
        AutonomousGroupChatRoundPlan previousRound,
        bool previousRoundNaturallyClosed,
        double randomRoll)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(previousRound);

        double relationshipModifier =
            plan.Decision.AverageRelationshipScore switch
            {
                < 40 => 0.82,
                >= 72 when plan.Decision.WeakestRelationshipScore >= 55 => 1.08,
                _ => 1
            };
        double participationModifier = previousRoundNaturallyClosed
            ? 0
            : previousRound.Speakers.Count switch
            {
                <= 1 => 0.8,
                >= 3 => 1.04,
                _ => 1
            };
        double retentionFactor = Math.Clamp(
            plan.ContinuationRatePercent / 100d
                * relationshipModifier
                * participationModifier,
            0,
            0.95);
        double occurrenceProbability = Math.Round(
            Math.Clamp(previousOccurrenceProbability, 0, 1)
                * retentionFactor,
            4,
            MidpointRounding.AwayFromZero);
        double boundedRoll = Math.Clamp(
            randomRoll,
            0,
            0.9999999999999999);

        return new AutonomousGroupChatContinuationDecision
        {
            RetentionFactor = retentionFactor,
            OccurrenceProbability = occurrenceProbability,
            RandomRoll = boundedRoll,
            ShouldContinue = boundedRoll < occurrenceProbability
        };
    }
}

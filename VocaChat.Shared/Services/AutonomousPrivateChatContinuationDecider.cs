using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 根据设置、双向关系和上一轮回应情况计算严格递减的下一轮概率。
/// </summary>
public sealed class AutonomousPrivateChatContinuationDecider
{
    public AutonomousPrivateChatContinuationDecision Decide(
        AutonomousPrivateChatPlan plan,
        double previousOccurrenceProbability,
        AutonomousPrivateChatRoundPlan previousRound,
        bool previousRoundNaturallyClosed,
        double randomRoll)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(previousRound);

        double boundedPreviousProbability = Math.Clamp(
            previousOccurrenceProbability,
            0,
            1);
        double boundedRoll = Math.Clamp(randomRoll, 0, 0.9999999999999999);
        double baseRetention = plan.ContinuationRatePercent / 100d;
        double relationshipModifier = plan.MutualRelationshipScore switch
        {
            < 35 => 0.8,
            >= 70 => 1.1,
            _ => 1
        };
        double contextModifier = previousRoundNaturallyClosed
            ? 0
            : previousRound.RecipientMessageMode switch
            {
                AutonomousPrivateChatMessageMode.None => 0.75,
                AutonomousPrivateChatMessageMode.Burst => 1.05,
                _ => 1
            };
        double retentionFactor = Math.Clamp(
            baseRetention * relationshipModifier * contextModifier,
            0,
            0.95);
        double occurrenceProbability = Math.Round(
            boundedPreviousProbability * retentionFactor,
            4,
            MidpointRounding.AwayFromZero);

        return new AutonomousPrivateChatContinuationDecision
        {
            RetentionFactor = retentionFactor,
            OccurrenceProbability = occurrenceProbability,
            RandomRoll = boundedRoll,
            ShouldContinue = boundedRoll < occurrenceProbability
        };
    }
}

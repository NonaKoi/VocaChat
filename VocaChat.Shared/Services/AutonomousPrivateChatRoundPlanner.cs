using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 结合双方方向关系和发起方主动性，选择普通轮中的消息簇形式。
/// </summary>
public sealed class AutonomousPrivateChatRoundPlanner
{
    public AutonomousPrivateChatRoundPlan Plan(
        AutonomousPrivateChatPlan plan,
        double initiatorModeRoll,
        double initiatorBurstSizeRoll,
        double recipientModeRoll,
        double recipientBurstSizeRoll)
    {
        ArgumentNullException.ThrowIfNull(plan);

        double initiatorBurstProbability = 0.3
            + GetInitiativeAdjustment(plan.InitiatorInitiativeLevel)
            + GetRelationshipBurstAdjustment(
                plan.InitiatorToRecipientRelationshipScore);
        initiatorBurstProbability = Math.Clamp(
            initiatorBurstProbability,
            0.05,
            0.75);
        bool initiatorUsesBurst =
            NormalizeRoll(initiatorModeRoll) < initiatorBurstProbability;

        (double noReplyProbability, double burstProbability) =
            GetRecipientProbabilities(
                plan.RecipientToInitiatorRelationshipScore);
        double normalizedRecipientRoll = NormalizeRoll(recipientModeRoll);
        AutonomousPrivateChatMessageMode recipientMode =
            normalizedRecipientRoll < noReplyProbability
                ? AutonomousPrivateChatMessageMode.None
                : normalizedRecipientRoll
                        < 1 - burstProbability
                    ? AutonomousPrivateChatMessageMode.Single
                    : AutonomousPrivateChatMessageMode.Burst;

        return new AutonomousPrivateChatRoundPlan
        {
            InitiatorMessageMode = initiatorUsesBurst
                ? AutonomousPrivateChatMessageMode.Burst
                : AutonomousPrivateChatMessageMode.Single,
            InitiatorMessageCount = initiatorUsesBurst
                ? GetBurstCount(initiatorBurstSizeRoll)
                : 1,
            RecipientMessageMode = recipientMode,
            RecipientMessageCount = recipientMode switch
            {
                AutonomousPrivateChatMessageMode.None => 0,
                AutonomousPrivateChatMessageMode.Single => 1,
                AutonomousPrivateChatMessageMode.Burst =>
                    GetBurstCount(recipientBurstSizeRoll),
                _ => 0
            }
        };
    }

    private static double GetInitiativeAdjustment(
        AutonomousInteractionInitiativeLevel initiativeLevel)
    {
        return initiativeLevel switch
        {
            AutonomousInteractionInitiativeLevel.Low => -0.15,
            AutonomousInteractionInitiativeLevel.High => 0.15,
            _ => 0
        };
    }

    private static double GetRelationshipBurstAdjustment(double score)
    {
        return score switch
        {
            < 35 => -0.1,
            >= 65 => 0.1,
            _ => 0
        };
    }

    private static (double NoReply, double Burst) GetRecipientProbabilities(
        double relationshipScore)
    {
        return relationshipScore switch
        {
            < 35 => (0.35, 0.1),
            >= 65 => (0.05, 0.35),
            _ => (0.15, 0.2)
        };
    }

    private static int GetBurstCount(double roll)
    {
        return NormalizeRoll(roll) < 0.65 ? 2 : 3;
    }

    private static double NormalizeRoll(double roll)
    {
        return Math.Clamp(roll, 0, 0.9999999999999999);
    }
}

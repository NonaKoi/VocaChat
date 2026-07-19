using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 根据最后一轮内容和双向关系，在四种收束形式中选择一次最终动作。
/// </summary>
public sealed class AutonomousPrivateChatClosurePlanner
{
    public AutonomousPrivateChatRoundPlan Plan(
        AutonomousPrivateChatPlan plan,
        AutonomousPrivateChatRoundPlan previousRound,
        string lastMessageContent,
        double modeRoll,
        double initiatorBurstRoll,
        double recipientBurstRoll)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(previousRound);

        if (LooksNaturallyClosed(lastMessageContent))
        {
            return CreatePlan(
                AutonomousPrivateChatMessageMode.None,
                AutonomousPrivateChatMessageMode.None,
                initiatorBurstRoll,
                recipientBurstRoll);
        }

        double normalizedRoll = Math.Clamp(
            modeRoll,
            0,
            0.9999999999999999);
        (double noneThreshold, double initiatorOnlyThreshold,
            double recipientOnlyThreshold) = GetThresholds(
                plan,
                previousRound,
                lastMessageContent);

        if (normalizedRoll < noneThreshold)
        {
            return CreatePlan(
                AutonomousPrivateChatMessageMode.None,
                AutonomousPrivateChatMessageMode.None,
                initiatorBurstRoll,
                recipientBurstRoll);
        }

        if (normalizedRoll < initiatorOnlyThreshold)
        {
            return CreatePlan(
                SelectClosingMessageMode(initiatorBurstRoll),
                AutonomousPrivateChatMessageMode.None,
                initiatorBurstRoll,
                recipientBurstRoll);
        }

        if (normalizedRoll < recipientOnlyThreshold)
        {
            return CreatePlan(
                AutonomousPrivateChatMessageMode.None,
                SelectClosingMessageMode(recipientBurstRoll),
                initiatorBurstRoll,
                recipientBurstRoll);
        }

        return CreatePlan(
            SelectClosingMessageMode(initiatorBurstRoll),
            SelectClosingMessageMode(recipientBurstRoll),
            initiatorBurstRoll,
            recipientBurstRoll);
    }

    public bool LooksNaturallyClosed(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return true;
        }

        string[] closingPhrases =
        {
            "晚安", "先这样", "回头聊", "下次聊", "先忙", "再见"
        };
        return closingPhrases.Any(content.Contains);
    }

    private static (double None, double InitiatorOnly, double RecipientOnly)
        GetThresholds(
            AutonomousPrivateChatPlan plan,
            AutonomousPrivateChatRoundPlan previousRound,
            string lastMessageContent)
    {
        if (previousRound.RecipientMessageMode
            == AutonomousPrivateChatMessageMode.None)
        {
            return plan.MutualRelationshipScore >= 70
                ? (0.4, 0.7, 0.9)
                : (0.55, 0.9, 0.98);
        }

        if (lastMessageContent.EndsWith('?')
            || lastMessageContent.EndsWith('？'))
        {
            return (0.1, 0.55, 0.65);
        }

        return plan.MutualRelationshipScore >= 70
            ? (0.1, 0.3, 0.5)
            : (0.25, 0.6, 0.75);
    }

    private static AutonomousPrivateChatMessageMode SelectClosingMessageMode(
        double roll)
    {
        return Math.Clamp(roll, 0, 0.9999999999999999) < 0.8
            ? AutonomousPrivateChatMessageMode.Single
            : AutonomousPrivateChatMessageMode.Burst;
    }

    private static AutonomousPrivateChatRoundPlan CreatePlan(
        AutonomousPrivateChatMessageMode initiatorMode,
        AutonomousPrivateChatMessageMode recipientMode,
        double initiatorBurstRoll,
        double recipientBurstRoll)
    {
        return new AutonomousPrivateChatRoundPlan
        {
            InitiatorMessageMode = initiatorMode,
            RecipientMessageMode = recipientMode,
            InitiatorMessageCount = GetClosingMessageCount(
                initiatorMode,
                initiatorBurstRoll),
            RecipientMessageCount = GetClosingMessageCount(
                recipientMode,
                recipientBurstRoll)
        };
    }

    private static int GetClosingMessageCount(
        AutonomousPrivateChatMessageMode mode,
        double burstRoll)
    {
        return mode switch
        {
            AutonomousPrivateChatMessageMode.None => 0,
            AutonomousPrivateChatMessageMode.Single => 1,
            AutonomousPrivateChatMessageMode.Burst => 2,
            _ => 0
        };
    }
}

using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 集中计算自主私信判断与延续规则共同使用的有方向关系分。
/// </summary>
internal static class AutonomousPrivateChatRelationshipScoring
{
    internal static double Calculate(AiRelationship relationship)
    {
        double normalizedAffinity = (relationship.Affinity + 100) / 2d;
        return relationship.Familiarity * 0.3
            + normalizedAffinity * 0.4
            + relationship.Trust * 0.3;
    }

    internal static double CalculateMutual(
        double initiatorToRecipientScore,
        double recipientToInitiatorScore)
    {
        double lowerScore = Math.Min(
            initiatorToRecipientScore,
            recipientToInitiatorScore);
        double averageScore =
            (initiatorToRecipientScore + recipientToInitiatorScore) / 2d;

        return lowerScore * 0.6 + averageScore * 0.4;
    }
}

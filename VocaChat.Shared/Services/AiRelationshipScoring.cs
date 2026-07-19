using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 集中计算自主互动规则共同使用的有方向和双向关系分。
/// </summary>
internal static class AiRelationshipScoring
{
    internal static double Calculate(AiRelationship relationship)
    {
        double normalizedAffinity = (relationship.Affinity + 100) / 2d;
        return relationship.Familiarity * 0.3
            + normalizedAffinity * 0.4
            + relationship.Trust * 0.3;
    }

    internal static double CalculateMutual(
        double firstToSecondScore,
        double secondToFirstScore)
    {
        double lowerScore = Math.Min(
            firstToSecondScore,
            secondToFirstScore);
        double averageScore =
            (firstToSecondScore + secondToFirstScore) / 2d;

        return lowerScore * 0.6 + averageScore * 0.4;
    }
}

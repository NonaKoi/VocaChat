namespace VocaChat.Services;

/// <summary>
/// 表示一条用于轮次信息增量判断的已保存 AI 消息。
/// </summary>
internal sealed record ConversationInformationMessage(
    Guid SpeakerAiAccountId,
    string Content);

/// <summary>
/// 保存一轮消息相对同一发言者既往消息的信息增量结果。
/// </summary>
internal sealed record ConversationInformationGainAssessment(
    double Score,
    bool IsLowInformation);

/// <summary>
/// 使用确定性文本相似度统一检查消息重复和自主会话信息增量，
/// 不调用模型，也不承担事实或情感语义判断。
/// </summary>
internal static class ConversationInformationGainEvaluator
{
    private const double NearDuplicateThreshold = 0.68;
    private const double PlanRepetitionThreshold = 0.66;
    private const double LowInformationThreshold = 0.38;

    public static bool IsNearDuplicate(string first, string second)
    {
        return !HasMeaningfulPolarityChange(first, second)
            && GetSimilarity(first, second) >= NearDuplicateThreshold;
    }

    public static bool RepeatsPlanPoint(string contribution, string coveredPoint)
    {
        return !HasMeaningfulPolarityChange(contribution, coveredPoint)
            && GetSimilarity(contribution, coveredPoint)
                >= PlanRepetitionThreshold;
    }

    /// <summary>
    /// 只与同一发言者的更早消息比较，避免把正常回应对方的问题误判为复述。
    /// </summary>
    public static ConversationInformationGainAssessment AssessRound(
        IReadOnlyList<ConversationInformationMessage> currentRound,
        IReadOnlyList<ConversationInformationMessage> previousRounds)
    {
        ArgumentNullException.ThrowIfNull(currentRound);
        ArgumentNullException.ThrowIfNull(previousRounds);

        List<double> noveltyScores = new();
        foreach (ConversationInformationMessage current in currentRound
                     .Where(message =>
                         !string.IsNullOrWhiteSpace(message.Content)))
        {
            IReadOnlyList<ConversationInformationMessage> comparable =
                previousRounds
                    .Where(previous =>
                        previous.SpeakerAiAccountId
                            == current.SpeakerAiAccountId
                        && !string.IsNullOrWhiteSpace(previous.Content))
                    .ToList()
                    .AsReadOnly();
            if (comparable.Count == 0)
            {
                noveltyScores.Add(1);
                continue;
            }

            double highestSimilarity = comparable.Max(previous =>
                HasMeaningfulPolarityChange(
                    current.Content,
                    previous.Content)
                    ? 0
                    : GetSimilarity(current.Content, previous.Content));
            noveltyScores.Add(1 - highestSimilarity);
        }

        if (noveltyScores.Count == 0)
        {
            return new ConversationInformationGainAssessment(0, true);
        }

        double score = Math.Round(
            noveltyScores.Average(),
            4,
            MidpointRounding.AwayFromZero);
        bool hasComparableHistory = currentRound.Any(current =>
            previousRounds.Any(previous =>
                previous.SpeakerAiAccountId == current.SpeakerAiAccountId));
        return new ConversationInformationGainAssessment(
            score,
            hasComparableHistory && score < LowInformationThreshold);
    }

    internal static double GetSimilarity(string first, string second)
    {
        string normalizedFirst = Normalize(first);
        string normalizedSecond = Normalize(second);
        if (normalizedFirst.Length == 0 || normalizedSecond.Length == 0)
        {
            return 0;
        }

        if (string.Equals(
                normalizedFirst,
                normalizedSecond,
                StringComparison.Ordinal))
        {
            return 1;
        }

        int shorterLength = Math.Min(
            normalizedFirst.Length,
            normalizedSecond.Length);
        if (shorterLength < 8)
        {
            return 0;
        }

        if (normalizedFirst.Contains(
                normalizedSecond,
                StringComparison.Ordinal)
            || normalizedSecond.Contains(
                normalizedFirst,
                StringComparison.Ordinal))
        {
            return (double)shorterLength
                / Math.Max(normalizedFirst.Length, normalizedSecond.Length);
        }

        HashSet<string> firstBigrams = CreateCharacterNGrams(
            normalizedFirst,
            2);
        HashSet<string> secondBigrams = CreateCharacterNGrams(
            normalizedSecond,
            2);
        int sharedCount = firstBigrams.Intersect(secondBigrams).Count();
        return 2d * sharedCount
            / (firstBigrams.Count + secondBigrams.Count);
    }

    private static bool HasMeaningfulPolarityChange(
        string first,
        string second)
    {
        return ContainsNegation(first) != ContainsNegation(second);
    }

    private static bool ContainsNegation(string value)
    {
        string[] markers =
        {
            "不", "没", "没有", "并非", "不是", "不会", "不能", "别", "未"
        };
        return markers.Any(marker => value.Contains(
            marker,
            StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> CreateCharacterNGrams(
        string value,
        int size)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        for (int index = 0; index <= value.Length - size; index++)
        {
            result.Add(value.Substring(index, size));
        }

        return result;
    }

    private static string Normalize(string value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}

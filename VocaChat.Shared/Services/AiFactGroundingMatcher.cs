using System.Text;

namespace VocaChat.Services;

/// <summary>
/// 使用确定性的文本片段重合检查生成事实是否有已知来源，不承担语义推理。
/// </summary>
internal static class AiFactGroundingMatcher
{
    public static bool HasGroundingOverlap(string claim, string source)
    {
        return HasOverlap(claim, source, maximumRequiredOverlap: 3);
    }

    /// <summary>
    /// 判断两段文本是否在谈论同一事实主题。该判断比事实依据校验更宽，
    /// 只用于从受保护记忆中找到当前话题，不能证明回复已经陈述完整事实。
    /// </summary>
    public static bool HasTopicOverlap(string claim, string source)
    {
        return HasOverlap(claim, source, maximumRequiredOverlap: 2);
    }

    private static bool HasOverlap(
        string claim,
        string source,
        int maximumRequiredOverlap)
    {
        string normalizedClaim = Normalize(claim);
        string normalizedSource = Normalize(source);
        if (normalizedClaim.Length == 0 || normalizedSource.Length == 0)
        {
            return false;
        }

        if (normalizedClaim.Contains(normalizedSource, StringComparison.Ordinal)
            || normalizedSource.Contains(normalizedClaim, StringComparison.Ordinal))
        {
            return true;
        }

        HashSet<string> sourceFragments = GetFragments(normalizedSource);
        int overlapCount = GetFragments(normalizedClaim)
            .Count(sourceFragments.Contains);
        int requiredOverlap = Math.Min(
            maximumRequiredOverlap,
            Math.Max(1, sourceFragments.Count / 3));
        return overlapCount >= requiredOverlap;
    }

    private static HashSet<string> GetFragments(string value)
    {
        HashSet<string> fragments = new(StringComparer.Ordinal);
        int fragmentLength = value.Any(character => character > 127) ? 2 : 4;
        if (value.Length <= fragmentLength)
        {
            fragments.Add(value);
            return fragments;
        }

        for (int index = 0; index <= value.Length - fragmentLength; index++)
        {
            fragments.Add(value.Substring(index, fragmentLength));
        }

        return fragments;
    }

    private static string Normalize(string value)
    {
        StringBuilder builder = new();
        foreach (char character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}

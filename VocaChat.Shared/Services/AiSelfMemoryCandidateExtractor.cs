using System.Security.Cryptography;
using System.Text;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 从已经正式保存的当前发言者消息中提取少量高价值个人记忆候选。
/// 这里只负责生成候选，候选仍需经过业务预验证和语义判断才能写入数据库。
/// </summary>
internal sealed class AiSelfMemoryCandidateExtractor
{
    private const int MaximumCandidateCount = 2;

    private static readonly CandidateRule[] Rules =
    {
        new(
            AiSelfMemoryType.Plan,
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Mutable,
            "本人消息包含可能需要跨轮延续的计划。",
            new[]
            {
                "计划", "打算", "准备", "决定", "之后要", "接下来要",
                "明天要", "下周要"
            }),
        new(
            AiSelfMemoryType.OngoingActivity,
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Mutable,
            "本人消息包含可能需要跨轮延续的当前活动。",
            new[]
            {
                "最近", "目前", "正在", "这阵子", "这段时间", "一直在"
            }),
        new(
            AiSelfMemoryType.Experience,
            AiSelfMemoryFactNature.Narrative,
            AiSelfMemoryMutability.Immutable,
            "本人消息包含可能具有长期价值的既往经历。",
            new[]
            {
                "上次", "之前", "曾经", "去过", "见过", "经历过",
                "刚从", "刚在", "那天", "小时候"
            }),
        new(
            AiSelfMemoryType.Preference,
            AiSelfMemoryFactNature.Subjective,
            AiSelfMemoryMutability.Evolving,
            "本人消息包含可能稳定延续的偏好或态度。",
            new[]
            {
                "喜欢", "偏爱", "更喜欢", "讨厌", "不喜欢", "更在意",
                "习惯"
            })
    };

    /// <summary>
    /// 在读取数据库上下文前执行廉价筛选，普通寒暄不会触发语义判断流程。
    /// </summary>
    public bool HasPotentialCandidate(
        IReadOnlyList<AiPersistedMessageEvidence> savedMessages)
    {
        return savedMessages.Any(message =>
            FindRule(message.Content) is not null);
    }

    /// <summary>
    /// 每轮最多返回两项候选，并跳过与现有有效记忆语义片段明显重复的内容。
    /// </summary>
    public IReadOnlyList<AiSelfMemoryProposal> Extract(
        AiMessageGenerationRequest request,
        IReadOnlyList<AiConversationSelfMemory> activeMemories,
        IReadOnlyList<AiPersistedMessageEvidence> savedMessages)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(activeMemories);
        ArgumentNullException.ThrowIfNull(savedMessages);

        List<AiSelfMemoryProposal> proposals = new();
        HashSet<string> acceptedSummaries =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (AiPersistedMessageEvidence message in savedMessages
                     .OrderBy(item => item.SentAt)
                     .ThenBy(item => item.MessageId))
        {
            string summary = Truncate(message.Content.Trim());
            CandidateRule? rule = FindRule(summary);
            if (rule is null
                || !acceptedSummaries.Add(summary)
                || activeMemories.Any(memory =>
                    memory.Type == rule.Type
                    && AiFactGroundingMatcher.HasGroundingOverlap(
                        memory.Summary,
                        summary)))
            {
                continue;
            }

            proposals.Add(new AiSelfMemoryProposal(
                AiSelfMemoryProposalOperation.Add,
                null,
                request.Speaker.Id,
                request.Speaker.CharacterWorldId,
                rule.Type,
                CreateAutomaticFactKey(rule.Type, summary),
                rule.FactNature,
                rule.Mutability,
                summary,
                rule.Reason));
            if (proposals.Count == MaximumCandidateCount)
            {
                break;
            }
        }

        return proposals.AsReadOnly();
    }

    private static CandidateRule? FindRule(string content)
    {
        string normalized = content?.Trim() ?? string.Empty;
        if (normalized.Length < 4)
        {
            return null;
        }

        int ongoingPreparationIndex = normalized.IndexOf(
            "正在准备",
            StringComparison.OrdinalIgnoreCase);
        if (ongoingPreparationIndex >= 0
            && !RefersToAnotherPerson(
                normalized,
                ongoingPreparationIndex))
        {
            return Rules.Single(rule =>
                rule.Type == AiSelfMemoryType.OngoingActivity);
        }

        foreach (CandidateRule rule in Rules)
        {
            foreach (string marker in rule.Markers)
            {
                int markerIndex = normalized.IndexOf(
                    marker,
                    StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0
                    || RefersToAnotherPerson(normalized, markerIndex))
                {
                    continue;
                }

                return rule;
            }
        }

        return null;
    }

    private static bool RefersToAnotherPerson(
        string content,
        int markerIndex)
    {
        int prefixStart = Math.Max(0, markerIndex - 2);
        string immediatePrefix = content[prefixStart..markerIndex];
        if (immediatePrefix.Contains('你')
            || immediatePrefix.Contains('他')
            || immediatePrefix.Contains('她')
            || immediatePrefix.Contains('它'))
        {
            return true;
        }

        bool asksAboutOtherPerson = content.Contains('你')
            && (content.EndsWith('?') || content.EndsWith('？'));
        return asksAboutOtherPerson && !content.Contains('我');
    }

    private static string CreateAutomaticFactKey(
        AiSelfMemoryType type,
        string summary)
    {
        string source =
            $"{type}:{summary.Trim().ToLowerInvariant()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        string shortHash = Convert.ToHexString(hash[..8]).ToLowerInvariant();
        return $"auto.{type.ToString().ToLowerInvariant()}.{shortHash}";
    }

    private static string Truncate(string value)
    {
        return value.Length <= AiSelfMemory.SummaryMaxLength
            ? value
            : value[..AiSelfMemory.SummaryMaxLength];
    }

    private sealed record CandidateRule(
        AiSelfMemoryType Type,
        AiSelfMemoryFactNature FactNature,
        AiSelfMemoryMutability Mutability,
        string Reason,
        IReadOnlyList<string> Markers);
}

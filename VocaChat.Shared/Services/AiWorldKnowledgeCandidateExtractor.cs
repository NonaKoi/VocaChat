using System.Security.Cryptography;
using System.Text;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 从一条已保存消息中提取保守、可重复验证的世界知识候选。
/// 当前只处理明确措辞和常见实体陈述，不调用模型补充消息中没有的信息。
/// </summary>
public sealed class AiWorldKnowledgeCandidateExtractor
{
    private readonly IAiWorldKnowledgeSemanticExtractor? _semanticExtractor;

    private static readonly string[] ExplicitCrossWorldMarkers =
    {
        "不在同一个世界",
        "不在一个世界",
        "来自不同世界",
        "身处不同世界",
        "我们不是一个世界的",
        "跨世界联系",
        "跨世界通信"
    };

    private static readonly string[] ParallelWorldMarkers =
    {
        "平行世界",
        "多个世界",
        "其他世界存在",
        "另一个世界真实存在",
        "跨世界"
    };

    private static readonly string[] BackgroundMarkers =
    {
        "我这里",
        "我们这里",
        "我这边",
        "我们这边",
        "你那里",
        "你们那里",
        "你那边",
        "你们那边"
    };

    private static readonly string[] DifferenceMarkers =
    {
        "不一样",
        "不同",
        "没听过",
        "没有这种",
        "规则不",
        "常识不",
        "完全不懂"
    };

    private static readonly string[] EntityMarkers =
    {
        "学院",
        "学园",
        "学校",
        "高中",
        "大学",
        "城市",
        "地区",
        "大陆",
        "星球",
        "帝国",
        "王国",
        "联邦",
        "共和国",
        "军团",
        "舰队",
        "组织",
        "教会",
        "公司",
        "自治区",
        "沙漠化"
    };

    private static readonly IReadOnlyDictionary<string, string>
        EntityCategories = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["学院"] = "school",
            ["学园"] = "school",
            ["学校"] = "school",
            ["高中"] = "school",
            ["大学"] = "school",
            ["城市"] = "place",
            ["地区"] = "place",
            ["大陆"] = "place",
            ["星球"] = "place",
            ["帝国"] = "polity",
            ["王国"] = "polity",
            ["联邦"] = "polity",
            ["共和国"] = "polity",
            ["军团"] = "organization",
            ["舰队"] = "organization",
            ["组织"] = "organization",
            ["教会"] = "organization",
            ["公司"] = "organization",
            ["自治区"] = "polity",
            ["沙漠化"] = "environment"
        };

    private static readonly string[] TrailingConceptConnectors =
    {
        "是一个",
        "是一所",
        "是一座",
        "是个",
        "属于",
        "位于",
        "受到",
        "发生了",
        "有严重的",
        "有",
        "的"
    };

    private static readonly string[] ConceptBoundaryConnectors =
    {
        "是一所",
        "是一个",
        "是一座",
        "是个",
        "属于",
        "位于",
        "受到",
        "发生了",
        "有严重的"
    };

    private static readonly string[] LeadingConceptConnectors =
    {
        "我们这里的",
        "我这里的",
        "我们那边的",
        "我那边的",
        "我住在",
        "我来自",
        "我在",
        "那个",
        "这个",
        "那所",
        "这所"
    };

    private static readonly HashSet<string> IgnoredMessages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "你好",
            "您好",
            "嗨",
            "哈喽",
            "早上好",
            "中午好",
            "晚上好",
            "晚安",
            "谢谢",
            "嗯",
            "哦",
            "好"
        };

    public AiWorldKnowledgeCandidateExtractor()
    {
    }

    public AiWorldKnowledgeCandidateExtractor(
        IAiWorldKnowledgeSemanticExtractor semanticExtractor)
    {
        _semanticExtractor = semanticExtractor
            ?? throw new ArgumentNullException(nameof(semanticExtractor));
    }

    /// <summary>
    /// 每条消息只调用一次。用户消息可以产生认知信号，但没有明确
    /// AI 来源账号时不会凭空创建世界知识对象。
    /// </summary>
    public AiWorldKnowledgeExtraction Extract(
        AiAccount? sourceAiAccount,
        string content)
    {
        string normalizedContent = content?.Trim() ?? string.Empty;
        if (normalizedContent.Length < 2
            || IgnoredMessages.Contains(
                normalizedContent.TrimEnd('。', '！', '!', '？', '?')))
        {
            return AiWorldKnowledgeExtraction.None;
        }

        AiWorldKnowledgeSignal signal = DetectSignal(normalizedContent);
        if (signal == AiWorldKnowledgeSignal.None
            || sourceAiAccount is null)
        {
            return new AiWorldKnowledgeExtraction(
                signal,
                Array.Empty<AiWorldKnowledgeCandidate>());
        }

        return new AiWorldKnowledgeExtraction(
            signal,
            new[]
            {
                CreateCandidate(
                    sourceAiAccount,
                    normalizedContent,
                    signal,
                    topicSignature: null)
            });
    }

    /// <summary>
    /// 先执行确定性提取；只有调用方确认存在跨世界学习可能且规则没有
    /// 命中时，才调用一次语义提取器。
    /// </summary>
    public async Task<AiWorldKnowledgeExtraction> ExtractAsync(
        AiAccount? sourceAiAccount,
        string content,
        bool allowSemanticFallback,
        AiModelUsageCorrelation? usageCorrelation = null,
        CancellationToken cancellationToken = default)
    {
        AiWorldKnowledgeExtraction deterministic = Extract(
            sourceAiAccount,
            content);
        if (deterministic.Signal != AiWorldKnowledgeSignal.None
            || deterministic.Candidates.Count > 0
            || !allowSemanticFallback
            || _semanticExtractor is null)
        {
            return deterministic;
        }

        AiWorldKnowledgeSemanticExtractionResult semantic =
            await _semanticExtractor.ExtractAsync(
                new AiWorldKnowledgeSemanticExtractionRequest(
                    content.Trim(),
                    sourceAiAccount?.Id,
                    usageCorrelation),
                cancellationToken);
        if (!semantic.IsSuccess)
        {
            return new AiWorldKnowledgeExtraction(
                AiWorldKnowledgeSignal.None,
                Array.Empty<AiWorldKnowledgeCandidate>(),
                UsedSemanticExtractor: true,
                semantic.ErrorMessage);
        }

        if (semantic.Signal == AiWorldKnowledgeSignal.None
            || sourceAiAccount is null)
        {
            return new AiWorldKnowledgeExtraction(
                semantic.Signal,
                Array.Empty<AiWorldKnowledgeCandidate>(),
                UsedSemanticExtractor: true,
                ErrorMessage: null);
        }

        IReadOnlyList<string?> topicSignatures = semantic.Concepts.Count == 0
            ? new string?[] { null }
            : semantic.Concepts
                .Select(concept =>
                    $"{concept.Category.ToString().ToLowerInvariant()}:"
                    + NormalizeTopic(concept.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string?>()
                .ToList()
                .AsReadOnly();
        IReadOnlyList<AiWorldKnowledgeCandidate> candidates = topicSignatures
            .Select(topicSignature => CreateCandidate(
                sourceAiAccount,
                content.Trim(),
                semantic.Signal,
                topicSignature))
            .ToList()
            .AsReadOnly();

        return new AiWorldKnowledgeExtraction(
            semantic.Signal,
            candidates,
            UsedSemanticExtractor: true,
            ErrorMessage: null);
    }

    private static AiWorldKnowledgeSignal DetectSignal(string content)
    {
        if (ContainsAny(content, ExplicitCrossWorldMarkers))
        {
            return AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation;
        }

        if (ContainsAny(content, ParallelWorldMarkers))
        {
            return AiWorldKnowledgeSignal.ParallelWorldInformation;
        }

        if (ContainsAny(content, BackgroundMarkers)
            && ContainsAny(content, DifferenceMarkers))
        {
            return AiWorldKnowledgeSignal.BackgroundDifference;
        }

        return ContainsAny(content, EntityMarkers)
            ? AiWorldKnowledgeSignal.UnfamiliarConcept
            : AiWorldKnowledgeSignal.None;
    }

    private static bool ContainsAny(
        string content,
        IEnumerable<string> markers)
    {
        return markers.Any(marker =>
            content.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateSummary(
        string sourceDisplayName,
        string content)
    {
        string summary = $"{sourceDisplayName}提到：{content}";
        return summary.Length <= AiWorldKnowledge.SummaryMaxLength
            ? summary
            : summary[..AiWorldKnowledge.SummaryMaxLength];
    }

    private static string CreateKnowledgeKey(
        Guid subjectWorldId,
        AiWorldKnowledgeSignal signal,
        string content,
        string? topicSignature = null)
    {
        string topic = !string.IsNullOrWhiteSpace(topicSignature)
            ? topicSignature
            : signal switch
        {
            AiWorldKnowledgeSignal.ParallelWorldInformation =>
                "parallel-world-existence",
            AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation =>
                "cross-world-relationship",
            _ => ExtractTopicSignature(content)
        };
        string source = $"{subjectWorldId:N}:{signal}:{topic}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        string shortHash = Convert.ToHexString(hash[..10]).ToLowerInvariant();
        return $"conversation.{signal.ToString().ToLowerInvariant()}.{shortHash}";
    }

    private static AiWorldKnowledgeCandidate CreateCandidate(
        AiAccount sourceAiAccount,
        string content,
        AiWorldKnowledgeSignal signal,
        string? topicSignature)
    {
        return new AiWorldKnowledgeCandidate(
            sourceAiAccount.CharacterWorldId,
            sourceAiAccount.Id,
            CreateKnowledgeKey(
                sourceAiAccount.CharacterWorldId,
                signal,
                content,
                topicSignature),
            CreateSummary(sourceAiAccount.Nickname, content),
            AiWorldKnowledgeFactNature.Unconfirmed,
            signal is AiWorldKnowledgeSignal.ParallelWorldInformation
                or AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation
                ? AiWorldKnowledgeMutability.Constant
                : AiWorldKnowledgeMutability.Changeable,
            AiWorldKnowledgeTrustLevel.DirectStatement,
            GetSalience(signal),
            signal);
    }

    /// <summary>
    /// 从当前确定性提取器能够识别的实体类别中生成稳定主题签名。
    /// 同一名称的“高中/学校”等表达会归入相同主题；无法可靠识别时
    /// 才回退到规范化全文，避免把不同概念强行合并。
    /// </summary>
    private static string ExtractTopicSignature(string content)
    {
        foreach (string marker in EntityMarkers
                     .OrderBy(marker => content.IndexOf(
                         marker,
                         StringComparison.OrdinalIgnoreCase)))
        {
            int markerIndex = content.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            string concept = ExtractConceptBeforeMarker(
                content,
                markerIndex);
            string category = EntityCategories.GetValueOrDefault(
                marker,
                marker);
            string normalizedConcept = NormalizeTopic(concept);
            if (normalizedConcept.Length == 0)
            {
                normalizedConcept = NormalizeTopic(marker);
            }

            return $"{category}:{normalizedConcept}";
        }

        return $"message:{NormalizeTopic(content)}";
    }

    private static string ExtractConceptBeforeMarker(
        string content,
        int markerIndex)
    {
        string prefix = content[..markerIndex];
        int clauseStart = prefix.LastIndexOfAny(
            new[] { '。', '！', '？', '，', '；', ',', ';', '\n', '\r' });
        string concept = clauseStart >= 0
            ? prefix[(clauseStart + 1)..]
            : prefix;
        concept = concept.Trim();

        foreach (string connector in ConceptBoundaryConnectors
                     .OrderByDescending(value => value.Length))
        {
            int connectorIndex = concept.IndexOf(
                connector,
                StringComparison.OrdinalIgnoreCase);
            if (connectorIndex > 0)
            {
                concept = concept[..connectorIndex].Trim();
                break;
            }
        }

        foreach (string connector in TrailingConceptConnectors
                     .OrderByDescending(value => value.Length))
        {
            if (concept.EndsWith(
                    connector,
                    StringComparison.OrdinalIgnoreCase))
            {
                concept = concept[..^connector.Length].Trim();
                break;
            }
        }

        foreach (string connector in LeadingConceptConnectors
                     .OrderByDescending(value => value.Length))
        {
            if (concept.StartsWith(
                    connector,
                    StringComparison.OrdinalIgnoreCase))
            {
                concept = concept[connector.Length..].Trim();
                break;
            }
        }

        const int maximumConceptLength = 24;
        return concept.Length <= maximumConceptLength
            ? concept
            : concept[^maximumConceptLength..];
    }

    private static string NormalizeTopic(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static int GetSalience(AiWorldKnowledgeSignal signal)
    {
        return signal switch
        {
            AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation => 95,
            AiWorldKnowledgeSignal.ParallelWorldInformation => 90,
            AiWorldKnowledgeSignal.BackgroundDifference => 80,
            AiWorldKnowledgeSignal.UnfamiliarConcept => 70,
            _ => AiWorldKnowledge.MinimumSalience
        };
    }
}

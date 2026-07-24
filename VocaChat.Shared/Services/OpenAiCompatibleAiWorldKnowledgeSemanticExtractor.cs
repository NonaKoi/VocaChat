using System.Text;
using System.Text.Json;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 使用当前模型连接识别规则词表之外的世界知识表达。
/// 模型只负责分类和截取原文概念，业务代码仍负责作用域、摘要和持久化。
/// </summary>
public sealed class OpenAiCompatibleAiWorldKnowledgeSemanticExtractor
    : IAiWorldKnowledgeSemanticExtractor
{
    private const int MaximumConceptCount = 3;
    private const int MaximumConceptNameLength = 80;
    private const int MaximumPromptContentLength = 4000;
    private const string FailureMessage =
        "世界知识语义提取暂时不可用，本条消息已保存但没有形成新知识。";

    private static readonly HashSet<string> IgnoredConceptNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "我", "你", "他", "她", "我们", "你们", "他们",
            "今天", "昨天", "明天", "这里", "那里", "这个", "那个",
            "人物", "地点", "学校", "组织", "政体", "环境", "文化",
            "事件", "概念", "世界", "现实"
        };

    private readonly OpenAiCompatibleChatClient _chatClient;
    private readonly AiMessageGenerationOptions _options;

    public OpenAiCompatibleAiWorldKnowledgeSemanticExtractor(
        OpenAiCompatibleChatClient chatClient,
        AiMessageGenerationOptions options)
    {
        _chatClient = chatClient
            ?? throw new ArgumentNullException(nameof(chatClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AiWorldKnowledgeSemanticExtractionResult> ExtractAsync(
        AiWorldKnowledgeSemanticExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string normalizedContent = request.Content?.Trim() ?? string.Empty;
        if (normalizedContent.Length < 2)
        {
            return AiWorldKnowledgeSemanticExtractionResult.None;
        }

        string? validationError = null;

        try
        {
            for (int attempt = 0;
                 attempt <= _options.OutputValidationRetryCount;
                 attempt++)
            {
                string userPrompt = BuildUserPrompt(normalizedContent);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    userPrompt += Environment.NewLine
                        + $"上一次 json 结果无效：{validationError}"
                        + Environment.NewLine
                        + "请只依据同一条消息原文重新输出完整 json。";
                }

                string? content = await _chatClient.CompleteJsonAsync(
                    BuildSystemPrompt(),
                    userPrompt,
                    temperature: 0.1,
                    topP: 0.4,
                    maximumCompletionTokens: Math.Min(
                        _options.MaximumCompletionTokens,
                        384),
                    cancellationToken,
                    request.SourceAiAccountId,
                    request.UsageCorrelation?.CreateInvocationContext(
                        AiModelInvocationStage.WorldKnowledgeExtraction,
                        attempt + 1,
                        request.SourceAiAccountId));

                try
                {
                    return ParseAndValidate(content, normalizedContent);
                }
                catch (AiMessageGenerationException exception)
                {
                    validationError = exception.Message;
                }
                catch (JsonException)
                {
                    validationError = "返回内容不是可解析的 json 对象。";
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AiWorldKnowledgeSemanticExtractionResult.Failed(
                FailureMessage);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return AiWorldKnowledgeSemanticExtractionResult.Failed(
            string.IsNullOrWhiteSpace(validationError)
                ? FailureMessage
                : $"世界知识语义提取输出无效：{validationError}");
    }

    private static AiWorldKnowledgeSemanticExtractionResult ParseAndValidate(
        string? content,
        string sourceContent)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiMessageGenerationException(
                "模型没有返回世界知识语义提取结果。");
        }

        using JsonDocument document = JsonDocument.Parse(
            RemoveMarkdownCodeFence(content.Trim()));
        JsonElement root = document.RootElement;
        string signalText = GetRequiredString(root, "signal");
        if (!Enum.TryParse(
                signalText,
                ignoreCase: false,
                out AiWorldKnowledgeSignal signal))
        {
            throw new AiMessageGenerationException(
                "signal 不是允许的世界知识信号。");
        }

        if (!root.TryGetProperty("concepts", out JsonElement conceptsElement)
            || conceptsElement.ValueKind != JsonValueKind.Array
            || conceptsElement.GetArrayLength() > MaximumConceptCount)
        {
            throw new AiMessageGenerationException(
                $"concepts 必须是最多 {MaximumConceptCount} 项的数组。");
        }

        List<AiWorldKnowledgeSemanticConcept> concepts = new();
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement item in conceptsElement.EnumerateArray())
        {
            string rawName = GetRequiredString(item, "name").Trim();
            string categoryText = GetRequiredString(item, "category");
            string? name = FindGroundedConceptName(
                rawName,
                sourceContent);
            if (name is null)
            {
                throw new AiMessageGenerationException(
                    "每个概念名称都必须是消息原文中出现的有效连续文本。");
            }

            if (!Enum.TryParse(
                    categoryText,
                    ignoreCase: false,
                    out AiWorldKnowledgeConceptCategory category))
            {
                throw new AiMessageGenerationException(
                    "category 不是允许的概念类别。");
            }

            if (names.Add(name))
            {
                concepts.Add(new AiWorldKnowledgeSemanticConcept(
                    name,
                    category));
            }
        }

        if (signal == AiWorldKnowledgeSignal.None && concepts.Count > 0)
        {
            throw new AiMessageGenerationException(
                "没有知识信号时不能返回概念。");
        }

        if (signal == AiWorldKnowledgeSignal.UnfamiliarConcept
            && concepts.Count == 0)
        {
            throw new AiMessageGenerationException(
                "陌生概念信号必须返回至少一个原文概念。");
        }

        return new AiWorldKnowledgeSemanticExtractionResult(
            signal,
            concepts.AsReadOnly(),
            ErrorMessage: null);
    }

    /// <summary>
    /// 本地模型偶尔会在正确名称外添加引号、类别说明或短前后缀。
    /// 最终只保留模型输出与消息原文共有的最长连续文本，因此不会把模型补写内容写入知识。
    /// </summary>
    private static string? FindGroundedConceptName(
        string rawName,
        string sourceContent)
    {
        if (rawName.Length > MaximumConceptNameLength * 4)
        {
            return null;
        }

        if (IsValidGroundedName(rawName, sourceContent))
        {
            return rawName;
        }

        string unwrappedName = rawName.Trim(
            '"', '\'', '“', '”', '‘', '’', '「', '」', '『', '』',
            '《', '》', '〈', '〉', '【', '】', '(', ')', '（', '）',
            '[', ']', '：', ':');
        if (IsValidGroundedName(unwrappedName, sourceContent))
        {
            return unwrappedName;
        }

        string longestCommonText = FindLongestCommonText(
            unwrappedName,
            sourceContent);
        return IsValidGroundedName(longestCommonText, sourceContent)
            ? longestCommonText
            : null;
    }

    private static bool IsValidGroundedName(
        string name,
        string sourceContent)
    {
        return name.Length >= 2
            && name.Length <= MaximumConceptNameLength
            && name.Any(char.IsLetterOrDigit)
            && !IgnoredConceptNames.Contains(name)
            && sourceContent.Contains(
                name,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string FindLongestCommonText(
        string proposedName,
        string sourceContent)
    {
        if (proposedName.Length < 2 || sourceContent.Length < 2)
        {
            return string.Empty;
        }

        int[,] lengths = new int[
            proposedName.Length + 1,
            sourceContent.Length + 1];
        int bestLength = 0;
        int bestEndIndex = 0;

        for (int proposedIndex = 1;
             proposedIndex <= proposedName.Length;
             proposedIndex++)
        {
            for (int sourceIndex = 1;
                 sourceIndex <= sourceContent.Length;
                 sourceIndex++)
            {
                if (char.ToUpperInvariant(proposedName[proposedIndex - 1])
                    != char.ToUpperInvariant(sourceContent[sourceIndex - 1]))
                {
                    continue;
                }

                int currentLength =
                    lengths[proposedIndex - 1, sourceIndex - 1] + 1;
                lengths[proposedIndex, sourceIndex] = currentLength;
                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestEndIndex = sourceIndex;
                }
            }
        }

        return bestLength < 2
            ? string.Empty
            : sourceContent.Substring(
                bestEndIndex - bestLength,
                bestLength).Trim();
    }

    private static string BuildSystemPrompt() =>
        """
        你是 VocaChat 的世界知识语义分类器。
        唯一证据是用户消息中给出的“消息原文”，不得使用训练知识、角色常识或外部资料。
        你的任务只是判断原文是否表达了世界差异、平行世界、明确跨世界关系，或介绍了值得记住的新人物、地点、学校、组织、政体、环境、文化、事件和概念。

        signal 只能使用：
        None：普通聊天，没有需要形成世界知识的内容；
        UnfamiliarConcept：原文介绍了一个新概念，但没有明确证明不同世界；
        BackgroundDifference：原文明确表达环境或常识差异，但没有确认平行世界；
        ParallelWorldInformation：原文明示多个世界或不同现实确实存在；
        ExplicitCrossWorldConfirmation：原文明示当前参与者来自不同世界或正在跨世界通信。

        新名称绝不能仅凭“陌生”就升级为平行世界结论。
        concepts 最多三项。每项 name 必须逐字复制消息原文中连续出现的名称，不得改写、补全或翻译。
        category 只能使用 Person、Place、School、Organization、Polity、Environment、Culture、Event、Concept。
        不要返回摘要、账号、世界、可信度、解释或 Markdown。
        """;

    private static string BuildUserPrompt(string content)
    {
        string boundedContent = content.Length <= MaximumPromptContentLength
            ? content
            : content[..MaximumPromptContentLength];
        StringBuilder builder = new();
        builder.AppendLine("请分析下面这一条消息原文：");
        builder.AppendLine(JsonSerializer.Serialize(boundedContent));
        builder.AppendLine();
        builder.AppendLine(
            """只输出：{"signal":"None","concepts":[]}""");
        return builder.ToString();
    }

    private static string GetRequiredString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new AiMessageGenerationException(
                $"语义提取缺少 {propertyName}。");
        }

        return property.GetString()!;
    }

    private static string RemoveMarkdownCodeFence(string content)
    {
        if (!content.StartsWith("```", StringComparison.Ordinal))
        {
            return content;
        }

        int firstLineBreak = content.IndexOf('\n');
        int lastFence = content.LastIndexOf(
            "```",
            StringComparison.Ordinal);
        return firstLineBreak >= 0 && lastFence > firstLineBreak
            ? content[(firstLineBreak + 1)..lastFence].Trim()
            : content;
    }
}

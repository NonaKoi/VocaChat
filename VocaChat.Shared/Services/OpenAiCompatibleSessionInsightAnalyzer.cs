using System.Text;
using System.Text.Json;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 使用 OpenAI 兼容模型分析一次已结束 Session，并验证方向、证据和输出边界。
/// </summary>
public sealed class OpenAiCompatibleSessionInsightAnalyzer
    : ISessionInsightAnalyzer
{
    private const int MaximumDirectionReasonLength = 300;
    private const int MaximumMemoryCandidatesPerDirection = 3;
    private const int MaximumEvidenceMessages = 3;
    private const int MaximumPromptMessages = 80;
    private const int MaximumPromptMessageLength = 300;
    private const string FallbackReason =
        "Session 语义分析不可用，采用基础关系变化且不提取长期记忆。";

    private readonly OpenAiCompatibleChatClient _chatClient;
    private readonly AiMessageGenerationOptions _options;

    public OpenAiCompatibleSessionInsightAnalyzer(
        OpenAiCompatibleChatClient chatClient,
        AiMessageGenerationOptions options)
    {
        _chatClient = chatClient
            ?? throw new ArgumentNullException(nameof(chatClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<SessionInsightAnalysis> AnalyzeAsync(
        SessionInsightAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<PrivateMessage> promptMessages = request.Messages
            .Where(message => message.Id != Guid.Empty)
            .TakeLast(MaximumPromptMessages)
            .ToList()
            .AsReadOnly();

        if (promptMessages.Count == 0)
        {
            return SessionInsightAnalysis.Fallback(FallbackReason);
        }

        Dictionary<Guid, PrivateMessage> messagesById = promptMessages
            .ToDictionary(message => message.Id);
        string? validationError = null;

        try
        {
            for (int attempt = 0;
                 attempt <= _options.OutputValidationRetryCount;
                 attempt++)
            {
                string userPrompt = BuildUserPrompt(request, promptMessages);

                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    userPrompt += Environment.NewLine
                        + $"上一次 json 洞察无效：{validationError}"
                        + Environment.NewLine
                        + "请只使用给出的消息证据，重新输出完整 json 对象。";
                }

                string? content = await _chatClient.CompleteJsonAsync(
                    BuildSystemPrompt(),
                    userPrompt,
                    temperature: 0.2,
                    topP: 0.6,
                    maximumCompletionTokens: Math.Min(
                        _options.MaximumCompletionTokens,
                        768),
                    cancellationToken);

                try
                {
                    return ParseAndValidate(content, request, messagesById);
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
            return SessionInsightAnalysis.Fallback(FallbackReason);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return SessionInsightAnalysis.Fallback(FallbackReason);
    }

    private static SessionInsightAnalysis ParseAndValidate(
        string? content,
        SessionInsightAnalysisRequest request,
        IReadOnlyDictionary<Guid, PrivateMessage> messagesById)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiMessageGenerationException("模型没有返回 Session 洞察。");
        }

        using JsonDocument document = JsonDocument.Parse(
            RemoveMarkdownCodeFence(content.Trim()));
        JsonElement root = document.RootElement;
        DirectionalSessionInsight initiatorPerspective = ParseDirection(
            GetRequiredObject(root, "initiatorPerspective"),
            request.Recipient.Id,
            messagesById);
        DirectionalSessionInsight recipientPerspective = ParseDirection(
            GetRequiredObject(root, "recipientPerspective"),
            request.Initiator.Id,
            messagesById);

        return new SessionInsightAnalysis(
            initiatorPerspective,
            recipientPerspective,
            false,
            string.Empty);
    }

    private static DirectionalSessionInsight ParseDirection(
        JsonElement direction,
        Guid subjectAiAccountId,
        IReadOnlyDictionary<Guid, PrivateMessage> messagesById)
    {
        (RelationshipSignalPolarity affinityPolarity,
            RelationshipSignalStrength affinityStrength) = ParseSignal(
                GetRequiredObject(direction, "affinity"),
                "好感");
        (RelationshipSignalPolarity trustPolarity,
            RelationshipSignalStrength trustStrength) = ParseSignal(
                GetRequiredObject(direction, "trust"),
                "信任");
        string reason = GetRequiredString(direction, "reason");

        if (reason.Length > MaximumDirectionReasonLength)
        {
            throw new AiMessageGenerationException("关系变化原因过长。");
        }

        IReadOnlyList<Guid> relationshipEvidence = ParseEvidenceMessageIds(
            direction,
            "relationshipEvidenceMessageIds",
            messagesById,
            allowEmpty: true);
        bool hasSemanticSignal =
            affinityPolarity != RelationshipSignalPolarity.Neutral
            || trustPolarity != RelationshipSignalPolarity.Neutral;

        if (hasSemanticSignal && relationshipEvidence.Count == 0)
        {
            throw new AiMessageGenerationException("非中性关系信号必须提供消息证据。");
        }

        IReadOnlyList<SessionMemoryCandidate> memoryCandidates =
            ParseMemoryCandidates(
                direction,
                subjectAiAccountId,
                messagesById);
        return new DirectionalSessionInsight(
            affinityPolarity,
            affinityStrength,
            trustPolarity,
            trustStrength,
            reason,
            relationshipEvidence,
            memoryCandidates);
    }

    private static (
        RelationshipSignalPolarity Polarity,
        RelationshipSignalStrength Strength) ParseSignal(
        JsonElement signal,
        string signalName)
    {
        RelationshipSignalPolarity polarity = ParseEnum<
            RelationshipSignalPolarity>(signal, "polarity", signalName);
        RelationshipSignalStrength strength = ParseEnum<
            RelationshipSignalStrength>(signal, "strength", signalName);

        if ((polarity == RelationshipSignalPolarity.Neutral)
            != (strength == RelationshipSignalStrength.None))
        {
            throw new AiMessageGenerationException(
                $"{signalName}的中性极性必须与 None 强度同时出现。");
        }

        return (polarity, strength);
    }

    private static IReadOnlyList<SessionMemoryCandidate> ParseMemoryCandidates(
        JsonElement direction,
        Guid subjectAiAccountId,
        IReadOnlyDictionary<Guid, PrivateMessage> messagesById)
    {
        if (!direction.TryGetProperty("memories", out JsonElement memories)
            || memories.ValueKind != JsonValueKind.Array)
        {
            throw new AiMessageGenerationException("方向洞察缺少 memories 数组。");
        }

        if (memories.GetArrayLength() > MaximumMemoryCandidatesPerDirection)
        {
            throw new AiMessageGenerationException("单个方向的记忆候选不能超过三条。");
        }

        List<SessionMemoryCandidate> candidates = new();
        HashSet<string> summaries = new(StringComparer.OrdinalIgnoreCase);

        foreach (JsonElement memoryElement in memories.EnumerateArray())
        {
            try
            {
                AiMemoryType type = ParseEnum<AiMemoryType>(
                    memoryElement,
                    "type",
                    "记忆类型");
                string summary = GetRequiredString(memoryElement, "summary");

                if (summary.Length > AiMemory.SummaryMaxLength)
                {
                    continue;
                }

                SessionMemoryImportance importance = ParseEnum<
                    SessionMemoryImportance>(
                        memoryElement,
                        "importance",
                        "记忆重要度");
                IReadOnlyList<Guid> evidenceMessageIds =
                    ParseEvidenceMessageIds(
                        memoryElement,
                        "evidenceMessageIds",
                        messagesById,
                        allowEmpty: false);

                if (!EvidenceSupportsSubject(
                        type,
                        subjectAiAccountId,
                        evidenceMessageIds,
                        messagesById)
                    || !summaries.Add(summary))
                {
                    continue;
                }

                candidates.Add(new SessionMemoryCandidate(
                    type,
                    summary,
                    importance,
                    evidenceMessageIds));
            }
            catch (AiMessageGenerationException)
            {
                // 单条没有可靠证据的候选被丢弃，不影响同一方向的其他有效洞察。
            }
        }

        return candidates.AsReadOnly();
    }

    private static bool EvidenceSupportsSubject(
        AiMemoryType type,
        Guid subjectAiAccountId,
        IReadOnlyList<Guid> evidenceMessageIds,
        IReadOnlyDictionary<Guid, PrivateMessage> messagesById)
    {
        if (type is AiMemoryType.ImportantEvent
            or AiMemoryType.SharedExperience)
        {
            return evidenceMessageIds.Count > 0;
        }

        return evidenceMessageIds.Any(messageId =>
            messagesById[messageId].SenderAiAccountId == subjectAiAccountId);
    }

    private static IReadOnlyList<Guid> ParseEvidenceMessageIds(
        JsonElement parent,
        string propertyName,
        IReadOnlyDictionary<Guid, PrivateMessage> messagesById,
        bool allowEmpty)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement evidence)
            || evidence.ValueKind != JsonValueKind.Array)
        {
            throw new AiMessageGenerationException(
                $"洞察缺少 {propertyName} 数组。");
        }

        List<Guid> messageIds = new();

        foreach (JsonElement item in evidence.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || !Guid.TryParse(item.GetString(), out Guid messageId)
                || !messagesById.ContainsKey(messageId))
            {
                throw new AiMessageGenerationException("洞察引用了不属于当前 Session 的消息。");
            }

            if (!messageIds.Contains(messageId))
            {
                messageIds.Add(messageId);
            }
        }

        if ((!allowEmpty && messageIds.Count == 0)
            || messageIds.Count > MaximumEvidenceMessages)
        {
            throw new AiMessageGenerationException("洞察的消息证据数量无效。");
        }

        return messageIds.AsReadOnly();
    }

    private static JsonElement GetRequiredObject(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.Object)
        {
            throw new AiMessageGenerationException(
                $"Session 洞察缺少 {propertyName} 对象。");
        }

        return element;
    }

    private static TEnum ParseEnum<TEnum>(
        JsonElement parent,
        string propertyName,
        string displayName)
        where TEnum : struct, Enum
    {
        string value = GetRequiredString(parent, propertyName);

        if (!Enum.TryParse(value, ignoreCase: true, out TEnum parsed)
            || !Enum.IsDefined(parsed))
        {
            throw new AiMessageGenerationException($"{displayName}无效。");
        }

        return parsed;
    }

    private static string GetRequiredString(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            throw new AiMessageGenerationException(
                $"Session 洞察缺少 {propertyName}。");
        }

        string value = element.GetString()?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AiMessageGenerationException(
                $"Session 洞察中的 {propertyName} 不能为空。");
        }

        return value;
    }

    private static string BuildUserPrompt(
        SessionInsightAnalysisRequest request,
        IReadOnlyList<PrivateMessage> messages)
    {
        StringBuilder builder = new();
        builder.AppendLine($"Session 话题：{request.Session.Topic}");
        AppendAccount(builder, "发起者", request.Initiator);
        AppendAccount(builder, "接收者", request.Recipient);
        builder.AppendLine("当前 Session 消息（方括号内为唯一证据 Id）：");

        foreach (PrivateMessage message in messages)
        {
            builder.AppendLine(
                $"[{message.Id}] {message.SenderDisplayName}："
                + Truncate(message.Content, MaximumPromptMessageLength));
        }

        builder.AppendLine("initiatorPerspective 表示发起者如何看待接收者；recipientPerspective 表示接收者如何看待发起者。不要交换方向。");
        builder.AppendLine("只有消息直接支持的事实才能成为记忆，每条记忆必须列出一至三个 evidenceMessageIds。");
        builder.AppendLine("偏好、习惯、承诺和个人事实至少需要一条由记忆对象本人发送的证据；低重要度琐事应省略。每个方向最多三条记忆。");
        return builder.ToString();
    }

    private static void AppendAccount(
        StringBuilder builder,
        string role,
        AiAccount account)
    {
        builder.AppendLine($"{role}：{account.Nickname}");
        builder.AppendLine(
            $"{role}身份：{DisplayOrDefault(account.IdentityDescription)}");
        builder.AppendLine(
            $"{role}性格：{DisplayOrDefault(account.Personality)}");
    }

    private static string BuildSystemPrompt() =>
        string.Join(
            Environment.NewLine,
            "你是 VocaChat 的 Session 后处理分析器，只总结已经发生的互动，不续写对话。",
            "你不能直接返回关系数值，只能返回关系极性 polarity 和强度 strength。",
            "polarity 只能是 Neutral、Positive、Negative；strength 只能是 None、Low、Medium、High。",
            "Neutral 必须配合 None；非中性关系信号必须引用当前 Session 的 relationshipEvidenceMessageIds。",
            "记忆 type 只能是 ImportantEvent、Preference、Habit、Commitment、SharedExperience、PersonalFact。",
            "记忆 importance 只能是 Low、Medium、High；只保留以后聊天可能真正有用的事实。",
            "不得把推测、寒暄、一次性措辞或账号静态人设重复保存为记忆。",
            "两个方向分别判断，不得假设一方知道另一方没有表达的事实。",
            "严格输出 json 对象，不要输出 Markdown、聊天台词或额外解释。",
            "json 结构：{\"initiatorPerspective\":{\"affinity\":{\"polarity\":\"Neutral\",\"strength\":\"None\"},\"trust\":{\"polarity\":\"Neutral\",\"strength\":\"None\"},\"reason\":\"没有明确关系事件\",\"relationshipEvidenceMessageIds\":[],\"memories\":[]},\"recipientPerspective\":{\"affinity\":{\"polarity\":\"Neutral\",\"strength\":\"None\"},\"trust\":{\"polarity\":\"Neutral\",\"strength\":\"None\"},\"reason\":\"没有明确关系事件\",\"relationshipEvidenceMessageIds\":[],\"memories\":[]}}");

    private static string RemoveMarkdownCodeFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        int firstLineEnd = value.IndexOf('\n');
        int closingFence = value.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineEnd >= 0 && closingFence > firstLineEnd
            ? value[(firstLineEnd + 1)..closingFence].Trim()
            : value;
    }

    private static string DisplayOrDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "未填写" : value.Trim();

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength
            ? value
            : $"{value[..maximumLength]}…";
}

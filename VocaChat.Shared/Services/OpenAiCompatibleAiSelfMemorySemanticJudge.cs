using System.Text;
using System.Text.Json;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 使用当前账号解析后的模型连接批量判断导演记忆候选。
/// 模型只返回语义建议，不能直接修改数据库或绕过业务硬规则。
/// </summary>
public sealed class OpenAiCompatibleAiSelfMemorySemanticJudge
    : IAiSelfMemorySemanticJudge
{
    private const int MaximumProposalCount = 2;
    private const int MaximumReasonLength = 300;
    private const int MaximumPromptMemoryCount = 20;
    private const int MaximumPromptMessageCount = 4;
    private const int MaximumPromptTextLength = 500;
    private const string FallbackReason =
        "语义记忆判断暂时不可用，候选保留为待确认。";

    private readonly OpenAiCompatibleChatClient _chatClient;
    private readonly AiMessageGenerationOptions _options;

    public OpenAiCompatibleAiSelfMemorySemanticJudge(
        OpenAiCompatibleChatClient chatClient,
        AiMessageGenerationOptions options)
    {
        _chatClient = chatClient
            ?? throw new ArgumentNullException(nameof(chatClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AiSelfMemorySemanticJudgmentResult> JudgeAsync(
        AiSelfMemorySemanticJudgmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Proposals.Count == 0)
        {
            return AiSelfMemorySemanticJudgmentResult.Empty;
        }

        string? validationError = null;

        try
        {
            for (int attempt = 0;
                 attempt <= _options.OutputValidationRetryCount;
                 attempt++)
            {
                string userPrompt = BuildUserPrompt(request);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    userPrompt += Environment.NewLine
                        + $"上一次 json 判断无效：{validationError}"
                        + Environment.NewLine
                        + "请保持候选索引不变，重新输出完整 json 对象。";
                }

                string? content = await _chatClient.CompleteJsonAsync(
                    BuildSystemPrompt(),
                    userPrompt,
                    temperature: 0.15,
                    topP: 0.5,
                    maximumCompletionTokens: Math.Min(
                        _options.MaximumCompletionTokens,
                        512),
                    cancellationToken,
                    aiAccountId: request.Speaker.Id,
                    invocationContext: request.UsageCorrelation
                        ?.CreateInvocationContext(
                            AiModelInvocationStage.SelfMemoryJudgment,
                            attempt + 1,
                            request.Speaker.Id));

                try
                {
                    return ParseAndValidate(content, request);
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
            return AiSelfMemorySemanticJudgmentResult.Pending(
                request.Proposals,
                FallbackReason);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return AiSelfMemorySemanticJudgmentResult.Pending(
            request.Proposals,
            string.IsNullOrWhiteSpace(validationError)
                ? FallbackReason
                : $"语义判断输出无效：{validationError}");
    }

    private static AiSelfMemorySemanticJudgmentResult ParseAndValidate(
        string? content,
        AiSelfMemorySemanticJudgmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiMessageGenerationException(
                "模型没有返回语义记忆判断。");
        }

        using JsonDocument document = JsonDocument.Parse(
            RemoveMarkdownCodeFence(content.Trim()));
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("decisions", out JsonElement decisionsElement)
            || decisionsElement.ValueKind != JsonValueKind.Array
            || decisionsElement.GetArrayLength() != request.Proposals.Count)
        {
            throw new AiMessageGenerationException(
                "语义判断必须为每项候选返回一次决定。");
        }

        List<AiSelfMemorySemanticDecision> decisions = new();
        HashSet<int> claimedIndexes = new();

        foreach (JsonElement item in decisionsElement.EnumerateArray())
        {
            int proposalIndex = GetRequiredInt32(item, "proposalIndex");
            if (proposalIndex < 0
                || proposalIndex >= request.Proposals.Count
                || !claimedIndexes.Add(proposalIndex))
            {
                throw new AiMessageGenerationException(
                    "语义判断包含无效或重复的候选索引。");
            }

            AiSelfMemorySemanticOutcome outcome = ParseEnum<
                AiSelfMemorySemanticOutcome>(item, "outcome");
            AiSelfMemoryFactNature factNature = ParseEnum<
                AiSelfMemoryFactNature>(item, "factNature");
            AiSelfMemoryMutability mutability = ParseEnum<
                AiSelfMemoryMutability>(item, "mutability");
            string factKey = GetRequiredString(item, "factKey")
                .ToLowerInvariant();
            string reason = GetRequiredString(item, "reason");
            if (factKey.Length > AiSelfMemory.FactKeyMaxLength
                || reason.Length > MaximumReasonLength)
            {
                throw new AiMessageGenerationException(
                    "语义判断的事实键或原因超过长度限制。");
            }

            Guid? targetMemoryId = ParseOptionalGuid(
                item,
                "targetMemoryId");
            if (outcome is AiSelfMemorySemanticOutcome.Supersede
                    or AiSelfMemorySemanticOutcome.Archive
                && targetMemoryId is null)
            {
                throw new AiMessageGenerationException(
                    "替代或归档决定必须指定目标记忆。");
            }

            AiSelfMemoryProposal proposal =
                request.Proposals[proposalIndex];
            if (outcome is AiSelfMemorySemanticOutcome.Supersede
                    or AiSelfMemorySemanticOutcome.Archive)
            {
                AiConversationSelfMemory? targetMemory = request
                    .ActiveMemories
                    .SingleOrDefault(memory =>
                        memory.Id == targetMemoryId);
                if (targetMemory is null)
                {
                    throw new AiMessageGenerationException(
                        "替代或归档决定引用了不存在的目标记忆。");
                }

                if (targetMemory.IsProtectedFact)
                {
                    throw new AiMessageGenerationException(
                        "语义判断不能替代或归档受保护事实。");
                }

                if (!string.Equals(
                        factKey,
                        targetMemory.FactKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new AiMessageGenerationException(
                        "替代或归档决定必须沿用目标记忆的事实键。");
                }
            }
            else if (!string.Equals(
                         factKey,
                         proposal.FactKey,
                         StringComparison.OrdinalIgnoreCase))
            {
                throw new AiMessageGenerationException(
                    "语义判断不能把候选改写到无关事实键。");
            }

            decisions.Add(new AiSelfMemorySemanticDecision(
                proposalIndex,
                outcome,
                targetMemoryId,
                factKey,
                factNature,
                mutability,
                reason));
        }

        return new AiSelfMemorySemanticJudgmentResult(
            decisions
                .OrderBy(decision => decision.ProposalIndex)
                .ToList()
                .AsReadOnly(),
            false,
            string.Empty);
    }

    private static string BuildUserPrompt(
        AiSelfMemorySemanticJudgmentRequest request)
    {
        StringBuilder builder = new();
        builder.AppendLine($"当前发言账号 Id：{request.Speaker.Id}");
        builder.AppendLine($"当前发言者：{request.Speaker.Nickname}");
        builder.AppendLine(
            $"身份：{DisplayOrDefault(request.Speaker.IdentityDescription)}");
        builder.AppendLine(
            $"性格：{DisplayOrDefault(request.Speaker.Personality)}");
        builder.AppendLine($"当前角色世界 Id：{request.Speaker.CharacterWorldId}");
        builder.AppendLine(
            $"当前角色世界：{DisplayOrDefault(request.CharacterWorldName)}");
        builder.AppendLine(
            $"角色世界权威说明：{DisplayOrDefault(request.CharacterWorldDescription)}");

        builder.AppendLine("本轮已经正式保存、属于当前发言者的消息证据：");
        foreach (AiPersistedMessageEvidence message in request.SavedMessages
                     .Take(MaximumPromptMessageCount))
        {
            builder.AppendLine(
                $"- [{message.MessageId}] "
                + Truncate(message.Content, MaximumPromptTextLength));
        }

        builder.AppendLine("当前世界中的有效个人记忆：");
        if (request.ActiveMemories.Count == 0)
        {
            builder.AppendLine("（无）");
        }
        foreach (AiConversationSelfMemory memory in request.ActiveMemories
                     .OrderByDescending(memory => memory.IsProtectedFact)
                     .ThenByDescending(memory => memory.IsUserLocked)
                     .ThenByDescending(memory => memory.Salience)
                     .Take(MaximumPromptMemoryCount))
        {
            builder.AppendLine(
                $"- [{memory.Id}] [主体={memory.AiAccountId}] "
                + $"[世界={memory.CharacterWorldId}] "
                + $"[事实键={memory.FactKey}] "
                + $"[{memory.FactNature}/{memory.Mutability}/"
                + $"{memory.TrustLevel}/{memory.Source}] "
                + $"[受保护={memory.IsProtectedFact}] "
                + Truncate(memory.Summary, MaximumPromptTextLength));
        }

        builder.AppendLine("待判断候选：");
        foreach ((AiSelfMemoryProposal proposal, int index) in request
                     .Proposals
                     .Take(MaximumProposalCount)
                     .Select((proposal, index) => (proposal, index)))
        {
            builder.AppendLine(
                $"- [索引={index}] [意图={proposal.Operation}] "
                + $"[目标={proposal.TargetMemoryId?.ToString() ?? "null"}] "
                + $"[主体={proposal.SubjectAiAccountId}] "
                + $"[世界={proposal.CharacterWorldId}] "
                + $"[类型={proposal.Type}] "
                + $"[事实键={proposal.FactKey}] "
                + $"[{proposal.FactNature}/{proposal.Mutability}] "
                + $"摘要={Truncate(proposal.Summary, MaximumPromptTextLength)} "
                + $"原因={Truncate(proposal.Reason, MaximumReasonLength)}");
        }

        builder.AppendLine(
            "逐项返回决定。新叙事可作为 NarrativeCandidate 接受；即时情绪和证据不足内容使用 Pending 或 Reject。");
        builder.AppendLine(
            "如果同一事实键的可变化导演记忆被新内容合理更新，使用 Supersede 并填写旧记忆 Id；不再有效且无需新版本时使用 Archive。");
        builder.AppendLine(
            "Accept、Reject 和 Pending 必须原样返回候选 factKey；Supersede 和 Archive 必须返回目标记忆的 factKey。不能创造与候选语义无关的新事实键。");
        return builder.ToString();
    }

    private static string BuildSystemPrompt() =>
        string.Join(
            Environment.NewLine,
            "你是 VocaChat 的个人记忆语义判断器，只判断候选，不编写聊天台词，也不直接修改数据。",
            "候选消息已经保存，但模型自己的台词不能证明用户正典或恒定客观事实可以被修改。",
            "只处理当前发言账号自己的个人记忆，不能把其他角色、回应对象或本地用户的经历归给当前发言者。",
            "当前角色世界说明优先于你的既有知识；不同世界的事实不能互相覆盖。",
            "outcome 只能是 Accept、Reject、Supersede、Archive、Pending。",
            "Accept 用于值得长期保存、没有现有冲突的新候选。",
            "Supersede 只用于同一事实键的 Mutable 或 Evolving 旧导演记忆发生合理变化，必须返回旧记忆 Id。",
            "Archive 只用于候选明确表明旧导演记忆已经结束且无需新版本，必须返回旧记忆 Id。",
            "Reject 用于错误归属、与消息不符、无长期价值或明确违反世界边界的内容。",
            "Pending 用于证据不足、语义含混、短期情绪或无法可靠判断的内容；Pending 不会成为有效记忆。",
            "用户来源、用户锁定、UserCanon 和 Immutable 事实不可由你建议替代或归档。",
            "不能自由改写 factKey。Accept、Reject 和 Pending 必须沿用候选 factKey；Supersede 和 Archive 必须沿用目标记忆 factKey。",
            "你可以修正候选的 factNature 和 mutability，但不能改变候选主体或世界。",
            "严格输出 json：{\"decisions\":[{\"proposalIndex\":0,\"outcome\":\"Accept\",\"targetMemoryId\":null,\"factKey\":\"experience.mist-island-cafe\",\"factNature\":\"Narrative\",\"mutability\":\"Immutable\",\"reason\":\"消息明确表达了值得延续的本人经历\"}]}",
            "不要输出 Markdown 或额外解释。");

    private static TEnum ParseEnum<TEnum>(
        JsonElement parent,
        string propertyName)
        where TEnum : struct, Enum
    {
        string value = GetRequiredString(parent, propertyName);
        if (!Enum.TryParse(value, ignoreCase: true, out TEnum parsed)
            || !Enum.IsDefined(parsed))
        {
            throw new AiMessageGenerationException(
                $"语义判断中的 {propertyName} 无效。");
        }

        return parsed;
    }

    private static int GetRequiredInt32(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out int value))
        {
            throw new AiMessageGenerationException(
                $"语义判断中的 {propertyName} 必须是整数。");
        }

        return value;
    }

    private static string GetRequiredString(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new AiMessageGenerationException(
                $"语义判断中的 {propertyName} 不能为空。");
        }

        return element.GetString()!.Trim();
    }

    private static Guid? ParseOptionalGuid(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String
            || !Guid.TryParse(element.GetString(), out Guid value))
        {
            throw new AiMessageGenerationException(
                $"语义判断中的 {propertyName} 无效。");
        }

        return value;
    }

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

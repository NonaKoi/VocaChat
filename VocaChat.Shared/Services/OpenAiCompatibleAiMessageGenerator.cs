using System.Text;
using System.Text.Json;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 通过 OpenAI 兼容的 Chat Completions 接口生成消息文本。
/// </summary>
public sealed class OpenAiCompatibleAiMessageGenerator : IAiMessageGenerator
{
    private const int ContextMessageCharacterLimit = 600;
    private readonly OpenAiCompatibleChatClient _chatClient;
    private readonly AiMessageGenerationOptions _options;
    private readonly AiConversationContextBuilder _contextBuilder;

    public OpenAiCompatibleAiMessageGenerator(
        OpenAiCompatibleChatClient chatClient,
        AiMessageGenerationOptions options,
        AiConversationContextBuilder contextBuilder)
    {
        _chatClient = chatClient
            ?? throw new ArgumentNullException(nameof(chatClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _contextBuilder = contextBuilder
            ?? throw new ArgumentNullException(nameof(contextBuilder));
    }

    public async Task<IReadOnlyList<string>> GenerateMessagesAsync(
        AiMessageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ExpectedMessageCount == 0)
        {
            return Array.Empty<string>();
        }

        if (request.ExpectedMessageCount is < 0 or > 3)
        {
            throw new AiMessageGenerationException("单次生成的消息数量必须在 0 到 3 之间。");
        }

        if (request.ActionPlan is null)
        {
            throw new AiMessageGenerationException("本次 AI 消息缺少行为与表达计划。");
        }

        ValidateOptions();

        try
        {
            AiMessageGenerationException? validationError = null;

            for (int attempt = 0;
                 attempt <= _options.OutputValidationRetryCount;
                 attempt++)
            {
                string userPrompt = BuildUserPrompt(request);
                if (validationError is not null)
                {
                    userPrompt += Environment.NewLine
                        + $"上一次输出未满足要求：{validationError.Message}"
                        + Environment.NewLine
                        + $"请重新输出，messages 数组必须恰好包含 {request.ExpectedMessageCount} 条独立消息，"
                        + "每条都应完成它承担的表达内容。";
                }

                string? content = await _chatClient.CompleteJsonAsync(
                    BuildSystemPrompt(request),
                    userPrompt,
                    _options.Temperature,
                    _options.TopP,
                    _options.MaximumCompletionTokens,
                    cancellationToken);

                try
                {
                    return ParseAndValidateMessages(
                        content,
                        request);
                }
                catch (AiMessageGenerationException exception)
                {
                    validationError = exception;
                }
                catch (JsonException exception)
                {
                    validationError = new AiMessageGenerationException(
                        "AI 模型返回了无法解析的消息格式。",
                        exception);
                }
            }

            throw validationError
                ?? new AiMessageGenerationException("AI 模型输出验证失败。");
        }
        catch (AiMessageGenerationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new AiMessageGenerationException(
                "AI 模型返回了无法解析的消息格式。",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new AiMessageGenerationException(
                "AI 模型返回的响应缺少必要字段。",
                exception);
        }
    }

    private IReadOnlyList<string> ParseAndValidateMessages(
        string? content,
        AiMessageGenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiMessageGenerationException("AI 模型没有返回消息内容。");
        }

        string normalizedContent = RemoveMarkdownCodeFence(content.Trim());
        using JsonDocument contentDocument = JsonDocument.Parse(normalizedContent);

        if (!contentDocument.RootElement.TryGetProperty(
                "messages",
                out JsonElement messagesElement)
            || messagesElement.ValueKind != JsonValueKind.Array)
        {
            throw new AiMessageGenerationException(
                "AI 模型输出必须包含 messages 数组。");
        }

        List<string> messages = messagesElement
            .EnumerateArray()
            .Select(element => element.GetString()?.Trim() ?? string.Empty)
            .ToList();

        if (messages.Count != request.ExpectedMessageCount)
        {
            throw new AiMessageGenerationException(
                $"AI 模型应生成 {request.ExpectedMessageCount} 条消息，实际返回 {messages.Count} 条。");
        }

        if (messages.Any(string.IsNullOrWhiteSpace))
        {
            throw new AiMessageGenerationException("AI 模型返回了空白消息。");
        }

        if (messages.Any(message => message.Length > _options.MaximumGeneratedMessageLength))
        {
            throw new AiMessageGenerationException(
                $"AI 模型生成的单条消息不能超过 {_options.MaximumGeneratedMessageLength} 个字符。");
        }

        ValidateConversationShape(messages, request);

        return messages.AsReadOnly();
    }

    private static void ValidateConversationShape(
        IReadOnlyList<string> messages,
        AiMessageGenerationRequest request)
    {
        List<string> normalizedMessages = messages
            .Select(NormalizeForComparison)
            .ToList();
        if (normalizedMessages.Distinct(StringComparer.Ordinal).Count()
            != normalizedMessages.Count)
        {
            throw new AiMessageGenerationException(
                "同一批消息不能重复表达相同内容。");
        }

        HashSet<string> recentSpeakerMessages = request.RecentMessages
            .Where(message =>
                message.SenderType == MessageSenderType.AiAccount
                && message.SenderAiAccountId == request.Speaker.Id)
            .Select(message => NormalizeForComparison(message.Content))
            .ToHashSet(StringComparer.Ordinal);
        if (normalizedMessages.Any(recentSpeakerMessages.Contains))
        {
            throw new AiMessageGenerationException(
                "不能原样重复该好友最近已经发送过的消息。");
        }

        HashSet<string> otherParticipantMessages = request.RecentMessages
            .Where(message =>
                message.SenderType == MessageSenderType.User
                || message.SenderAiAccountId != request.Speaker.Id)
            .Select(message => NormalizeForComparison(message.Content))
            .Where(message => message.Length >= 8)
            .ToHashSet(StringComparer.Ordinal);
        if (normalizedMessages.Any(otherParticipantMessages.Contains))
        {
            throw new AiMessageGenerationException(
                "不能把其他参与者说过的具体内容原样当作自己的消息。");
        }

        IReadOnlyList<string> recentContents = request.RecentMessages
            .Select(message => message.Content)
            .ToList()
            .AsReadOnly();
        if (messages.Any(message => recentContents.Any(recent =>
                IsSemanticallyNearDuplicate(message, recent))))
        {
            throw new AiMessageGenerationException(
                "不能只换几个词重复最近已经表达过的内容，必须增加新的信息或态度变化。");
        }

        if (messages.Any(message => HasUnsupportedFirstPersonExperience(
                message,
                request)))
        {
            throw new AiMessageGenerationException(
                "当前好友没有可靠资料或本人历史支持这段第一人称经历。");
        }

        if (request.OtherParticipantHasResponded == false
            && messages.Any(ClaimsUnsupportedSharedConclusion))
        {
            throw new AiMessageGenerationException(
                "对方在本次交流中尚未回应，不能替对方表达选择、感受或共同结论。");
        }

        IReadOnlyList<string> otherParticipantContents = request.RecentMessages
            .Where(message =>
                message.SenderType == MessageSenderType.User
                || message.SenderAiAccountId != request.Speaker.Id)
            .Select(message => message.Content)
            .ToList()
            .AsReadOnly();
        if (messages.Any(message => HasLikelyBorrowedFirstPersonExperience(
                message,
                request,
                otherParticipantContents)))
        {
            throw new AiMessageGenerationException(
                "不能把其他参与者的具体经历改写成当前好友的第一人称经历。");
        }

        if (messages.Any(message => message.Contains(
                $"我是{request.Speaker.Nickname}",
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new AiMessageGenerationException(
                "日常聊天中不能重新进行自我介绍。");
        }

        string[] fixedServicePhrases =
        {
            "有什么我可以帮你",
            "还有什么想聊的",
            "有什么需要随时告诉我",
            "你愿意和我说说吗",
            "如果你愿意的话，可以"
        };
        if (messages.Any(message => fixedServicePhrases.Any(phrase =>
                message.Contains(phrase, StringComparison.OrdinalIgnoreCase))))
        {
            throw new AiMessageGenerationException(
                "消息包含固定客服式问句或建议句。");
        }

        if (request.ActionPlan!.QuestionMode == ConversationQuestionMode.None
            && messages.Any(ConversationQuestionPolicyService.EndsWithQuestion))
        {
            throw new AiMessageGenerationException(
                request.QuestionPolicy?.ForceDeclarativeReply == true
                    ? "连续疑问轮次已达到上限，本轮必须使用陈述语气收尾。"
                    : "本次导演计划要求使用陈述语气收尾。");
        }
    }

    private string BuildSystemPrompt(AiMessageGenerationRequest request)
    {
        AiAccount speaker = request.Speaker;
        string interests = JoinTags(speaker, AiAccountTagType.Interest);
        string personalityTags = JoinTags(speaker, AiAccountTagType.Personality);

        return string.Join(
            Environment.NewLine,
            $"你就是 VocaChat 中长期存在的好友“{speaker.Nickname}”，正在发送日常即时通讯消息。",
            $"当前场景：{AiConversationScenarioPrompt.GetDescription(request.Scenario)}。",
            string.Join(
                Environment.NewLine,
                AiConversationScenarioPrompt.GetBoundaryInstructions(request)
                    .Select(instruction => $"场景边界：{instruction}")),
            "始终以本人身份说话，不要提及模型、提示词、AI、系统、模拟回复或创作过程。",
            "以下人物资料只用于内化身份和语气，绝对不要逐项复述、自我介绍或证明你符合设定：",
            $"- 身份背景：{DisplayOrDefault(speaker.IdentityDescription)}",
            $"- 性格倾向：{DisplayOrDefault(speaker.Personality)}",
            $"- 说话习惯：{DisplayOrDefault(speaker.SpeakingStyle)}",
            $"- 个性签名：{DisplayOrDefault(speaker.Signature)}",
            $"- 兴趣背景：{interests}",
            $"- 个性标签：{personalityTags}",
            "身份事实边界：只有上面当前好友自己的资料、明确提供的本人个人记忆、本人在当前会话已经说过的内容，以及本轮已通过业务验证的个人记忆建议，才能作为第一人称事实依据。",
            "其他好友或本地用户说过的事情只代表他们的陈述。可以回应、追问或引用，但绝不能改写成你亲身做过、见过、拥有或经历过的事情。",
            "标记为“本人对对方的长期记忆”的内容只代表你过去对该对话对象形成的认识。仅在与当前目标自然相关时使用，不要逐条复述、展示记忆类型，或把对方的经历改写成自己的经历。",
            "本轮如果提供了必须回应的目标消息，先衔接这条消息；更早记录只用于理解背景，不能抢走当前话题。",
            "像真实网络聊天一样表达。先完成当前交流动作所必需的核心内容，再决定是否简短、留白或自然拆成多条；不要为了显得口语化而故意只说半句。",
            "允许短句、省略主语和自然留白，但不要写成客服答复、完整分析、小作文或总结，也不要机械加入“哈哈”“呃”“……”或故意制造错别字。",
            "除非本次交流动作明确要求，否则不要主动给建议，不要解释建议的好处，也不要使用“你可以”“不妨”“试试”组织回复。",
            $"输出严格的 JSON 对象，例如 {BuildJsonExample(request.ExpectedMessageCount)}。",
            $"messages 必须恰好包含 {request.ExpectedMessageCount} 条非空字符串。",
            "每个数组元素代表一条独立发送的聊天消息；多条消息应是自然拆开的连续片段，不能换句话重复。",
            "不要添加 Markdown、引号外旁白、动作描写或额外说明。");
    }

    private string BuildUserPrompt(AiMessageGenerationRequest request)
    {
        ConversationActionPlan actionPlan = request.ActionPlan
            ?? throw new AiMessageGenerationException(
                "本次 AI 消息缺少行为与表达计划。");
        StringBuilder builder = new();
        builder.AppendLine(
            $"场景：{AiConversationScenarioPrompt.GetDescription(request.Scenario)}");
        builder.AppendLine($"当前话题背景：{DisplayOrDefault(request.Topic)}");
        builder.AppendLine($"本次交流动作：{GetActionInstruction(actionPlan.Action)}");
        if (request.DirectionPlan is not null)
        {
            builder.AppendLine(
                $"当前会话节拍：{GetBeatInstruction(request.DirectionPlan.Beat)}");
            builder.AppendLine($"导演指定的话题焦点：{request.DirectionPlan.TopicFocus}");
            builder.AppendLine($"导演指定的回应目标：{request.DirectionPlan.ResponseGoal}");
            if (request.DirectionPlan.CoveredPoints.Count > 0)
            {
                builder.AppendLine(
                    $"最近已经表达过的内容（不要换句话重复）：{string.Join("、", request.DirectionPlan.CoveredPoints)}");
            }
            if (request.DirectionPlan.UnresolvedGoals.Count > 0)
            {
                builder.AppendLine(
                    $"原始要求中仍需处理的部分：{string.Join("、", request.DirectionPlan.UnresolvedGoals)}");
            }
            builder.AppendLine(
                $"本轮必须新增的内容：{request.DirectionPlan.NewContribution}");
            if (request.DirectionPlan.AvoidedTopics.Count > 0)
            {
                builder.AppendLine(
                    $"本轮不要恢复的话题：{string.Join("、", request.DirectionPlan.AvoidedTopics)}");
            }
            if (request.DirectionPlan.ForbiddenClaims.Count > 0)
            {
                builder.AppendLine(
                    $"本轮不得声称的事实：{string.Join("、", request.DirectionPlan.ForbiddenClaims)}");
            }
            if (request.DirectionPlan.SelfMemoryProposals.Count > 0)
            {
                builder.AppendLine("本轮已经通过业务验证、必须自然表达后才会保存的个人事实：");
                foreach (AiSelfMemoryProposal proposal in request.DirectionPlan
                             .SelfMemoryProposals)
                {
                    builder.AppendLine(
                        $"- [{proposal.Operation}] [{proposal.Type}] {proposal.Summary}");
                }
            }
        }
        if (actionPlan.Action == ConversationAction.Answer)
        {
            builder.AppendLine("完整性要求：先把当前问题或请求的核心信息交代清楚。可以简练，但不能为了短而只回答半步，让对方必须再次追问才能知道基本答案。");
        }
        builder.AppendLine($"消息长度：{GetLengthInstruction(actionPlan.MessageLength)}");
        builder.AppendLine($"回应直接程度：{GetDirectnessInstruction(actionPlan.Directness)}");
        builder.AppendLine($"提问方式：{GetQuestionInstruction(actionPlan.QuestionMode)}");
        builder.AppendLine($"情绪可见度：{GetEmotionInstruction(actionPlan.EmotionVisibility)}");
        builder.AppendLine($"话题移动：{GetTopicInstruction(actionPlan.TopicMovement)}");
        builder.AppendLine($"标点节奏：{GetPunctuationInstruction(actionPlan.PunctuationRhythm)}");
        builder.AppendLine($"关系距离：{GetRelationshipInstruction(actionPlan.RelationshipTone)}");
        builder.AppendLine($"关系对等感：{GetRelationshipBalanceInstruction(actionPlan.RelationshipBalance)}");

        if (request.RoundNumber is not null)
        {
            builder.AppendLine($"当前轮次：{request.RoundNumber}");
        }

        if (request.OtherParticipantHasResponded == false)
        {
            builder.AppendLine("事实约束：对方在本次交流中还没有实际回复。可以继续表达自己或自然收住，但不能声称对方已经同意、拒绝、选择或与你形成共识。");
        }

        if (request.OtherParticipants.Count > 0)
        {
            builder.AppendLine(
                $"对话对象：{string.Join("、", request.OtherParticipants.Select(account => account.Nickname))}");
        }

        if (request.PrimarySpeaker is not null)
        {
            builder.AppendLine($"此前主要发言者：{request.PrimarySpeaker.Nickname}");
        }

        AiConversationContext conversationContext = _contextBuilder.Build(
            request,
            _options.RecentMessageLimit);
        AppendReplyTarget(builder, request, conversationContext);
        AppendConversationAnchor(builder, request);
        AppendSelfMemories(builder, conversationContext.SelfMemories);
        AppendMemories(builder, conversationContext.Memories);

        builder.AppendLine("更早的最近消息（仅作背景，方括号内是严格的事实归属）：");
        if (conversationContext.Messages.Count == 0)
        {
            builder.AppendLine("（暂无）");
        }

        foreach (AiConversationContextMessage contextMessage in
                 conversationContext.Messages)
        {
            AiDialogueMessage message = contextMessage.Message;
            builder.AppendLine(
                $"{GetOwnershipLabel(contextMessage.Ownership, message.SenderDisplayName)} "
                + $"{message.SenderDisplayName}：{Truncate(message.Content, ContextMessageCharacterLimit)}");
        }


        if (conversationContext.Messages.Any(message =>
                message.Ownership !=
                    AiConversationMessageOwnership.CurrentSpeaker))
        {
            builder.AppendLine("引用其他参与者的具体地点、物品或经历时，必须保留“听起来”“你说的”“原来”等听闻距离，或者只表达自己的即时反应；不要用“确实”“就是这样”等无来源语气替别人确认事实。");
        }

        if (actionPlan.MayOmitObviousContext)
        {
            builder.AppendLine("上下文已经清楚的部分可以省略，不必复述对方刚说过的话。");
        }

        if (actionPlan.MayLeaveThoughtOpen
            && actionPlan.Action != ConversationAction.Answer)
        {
            builder.AppendLine("可以只说半步，不必把理由、结论和后续建议一次交代完整。");
        }

        if (actionPlan.TopicMovement == ConversationTopicMovement.Shift)
        {
            builder.AppendLine("如果需要转到邻近话题，只能从当前目标消息自然联想；不要从更早的背景消息中恢复无关旧话题。");
        }
        else
        {
            builder.AppendLine("不要从更早的背景消息中突然恢复旧话题；本轮只衔接目标消息里的当前内容。");
        }

        builder.AppendLine("只完成指定的一个交流动作，不要同时回答、总结、安慰、建议并追问。除非计划要求提问，否则不要用问题句硬续话题。");
        builder.AppendLine("如果想分享自己的经历，但上下文和本人资料中没有可靠依据，就改为表达即时感受、一般看法或追问，不得借用别人刚说的具体经历。");
        builder.AppendLine("如果本轮具有已验证的个人记忆建议，只有在消息中确实表达该事实后业务层才会保存；不要添加建议之外的新近况、新计划或新经历。");
        builder.AppendLine("与最近消息语义相同的附和、结论或经历，即使换了几个词也不算新内容；必须完成导演指定的新增内容。");
        return builder.ToString();
    }

    private static void AppendMemories(
        StringBuilder builder,
        IReadOnlyList<AiConversationMemory> memories)
    {
        builder.AppendLine("本人对当前对话对象的长期记忆（仅在自然相关时参考，不能替代本轮目标）：");

        if (memories.Count == 0)
        {
            builder.AppendLine("（暂无）");
            return;
        }

        foreach (AiConversationMemory memory in memories)
        {
            builder.AppendLine(
                $"[{GetMemoryTypeDescription(memory.Type)}] 关于{memory.SubjectDisplayName}："
                + $"{Truncate(memory.Summary, ContextMessageCharacterLimit)}"
                + $"（{memory.OccurredAt:yyyy-MM-dd}）");
        }
    }

    private static void AppendSelfMemories(
        StringBuilder builder,
        IReadOnlyList<AiConversationSelfMemory> memories)
    {
        builder.AppendLine("本人有效个人记忆（可作为第一人称事实，但不要逐条复述）：");
        if (memories.Count == 0)
        {
            builder.AppendLine("（暂无）");
            return;
        }

        foreach (AiConversationSelfMemory memory in memories)
        {
            builder.AppendLine(
                $"[{memory.Id}] [{memory.Type}] "
                + Truncate(memory.Summary, ContextMessageCharacterLimit));
        }
    }

    private static string GetMemoryTypeDescription(AiMemoryType type) =>
        type switch
        {
            AiMemoryType.ImportantEvent => "重要事件",
            AiMemoryType.Preference => "偏好",
            AiMemoryType.Habit => "习惯",
            AiMemoryType.Commitment => "承诺",
            AiMemoryType.SharedExperience => "共同经历",
            AiMemoryType.PersonalFact => "个人事实",
            _ => "长期记忆"
        };

    private static void AppendConversationAnchor(
        StringBuilder builder,
        AiMessageGenerationRequest request)
    {
        AiDialogueMessage? anchor = request.ConversationAnchor;
        if (anchor is null
            || anchor.MessageId != Guid.Empty
                && anchor.MessageId == request.ReplyTarget?.Message?.MessageId)
        {
            return;
        }

        builder.AppendLine("整轮互动的原始用户要求（回应上一位发言者时也不能遗忘）：");
        builder.AppendLine(
            $"[本地用户原始消息] {anchor.SenderDisplayName}：{Truncate(anchor.Content, ContextMessageCharacterLimit)}");
        builder.AppendLine("如果原始消息包含多个步骤或要求，本轮应完成导演标记为尚未解决的部分。");
    }

    private static void AppendReplyTarget(
        StringBuilder builder,
        AiMessageGenerationRequest request,
        AiConversationContext conversationContext)
    {
        builder.AppendLine("本轮必须完成的对话目标：");

        switch (request.ReplyTarget?.Kind)
        {
            case AiDialogueReplyTargetKind.Message:
                AppendTargetMessage(builder, conversationContext.ReplyTarget);
                builder.AppendLine("先回应上面这条目标消息的核心内容，不要改去接更早的一句。");
                break;
            case AiDialogueReplyTargetKind.TopicOpening:
                builder.AppendLine(
                    $"没有需要回答的上一句；围绕“{DisplayOrDefault(request.Topic)}”自然开启本次交流。");
                break;
            case AiDialogueReplyTargetKind.TopicContinuation:
                builder.AppendLine(
                    $"对方刚才没有新回复；沿着“{DisplayOrDefault(request.Topic)}”补充一个自然的新片段，不要假装对方说了新内容。");
                break;
            case AiDialogueReplyTargetKind.ConversationClosing:
                AppendTargetMessage(builder, conversationContext.ReplyTarget);
                builder.AppendLine("结合当前对话自然收住，不开启新问题或无关话题。");
                break;
            default:
                builder.AppendLine(
                    $"衔接本轮触发文本：{DisplayOrDefault(request.FocusContent)}");
                break;
        }
    }

    private static void AppendTargetMessage(
        StringBuilder builder,
        AiConversationContextMessage? target)
    {
        if (target is null)
        {
            builder.AppendLine("（当前没有可回应的具体消息）");
            return;
        }

        AiDialogueMessage message = target.Message;
        builder.AppendLine(
            $"{GetOwnershipLabel(target.Ownership, message.SenderDisplayName)} "
            + $"{message.SenderDisplayName}：{Truncate(message.Content, ContextMessageCharacterLimit)}");
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new AiMessageGenerationException("未配置 AI 模型名称。");
        }

        if (_options.TimeoutSeconds <= 0
            || _options.RecentMessageLimit <= 0
            || _options.MaximumGeneratedMessageLength <= 0
            || _options.MaximumCompletionTokens <= 0
            || _options.OutputValidationRetryCount is < 0 or > 2)
        {
            throw new AiMessageGenerationException("AI 文本生成配置无效。");
        }
    }

    private static string GetOwnershipLabel(
        AiConversationMessageOwnership ownership,
        string senderDisplayName) =>
        ownership switch
        {
            AiConversationMessageOwnership.CurrentSpeaker
                => "[本人过去说过，可延续为自己的历史]",
            AiConversationMessageOwnership.OtherAiAccount
                => $"[其他好友“{senderDisplayName}”说过，只能视为听到的内容]",
            AiConversationMessageOwnership.LocalUser
                => "[本地用户说过，只能视为用户的信息]",
            _ => throw new ArgumentOutOfRangeException(nameof(ownership))
        };

    private static string GetActionInstruction(ConversationAction action) =>
        action switch
        {
            ConversationAction.Acknowledge => "接住对方的话，给出简短回应，不展开完整论述",
            ConversationAction.Answer => "直接回应目标消息里的核心问题或明确请求，先给出对方正在等的内容，不绕去讲别的话题",
            ConversationAction.Ask => "只追问一个此刻真正想知道的小点",
            ConversationAction.Share => "分享一个直接相关的想法、感受或小经历",
            ConversationAction.React => "先给出即时反应，可以很短，不负责把事情讲完",
            ConversationAction.Comfort => "以陪伴和理解为主，不急着分析原因或提供方案",
            ConversationAction.Tease => "在关系允许的范围内轻轻调侃，不攻击也不解释笑点",
            ConversationAction.Disagree => "表达一点真实分歧，只指出核心不同，不写辩论稿",
            ConversationAction.Evade => "不完全正面回答，可以含糊带过，但仍保持人物一致",
            ConversationAction.ShiftTopic => "自然带到邻近话题，不要使用生硬的转场句",
            ConversationAction.Close => "顺着已有对话轻轻收住，不开启新问题，也不必正式告别",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

    private static string GetBeatInstruction(ConversationBeat beat) =>
        beat switch
        {
            ConversationBeat.Introduce => "引入：建立一个明确起点，不急着把结论说完",
            ConversationBeat.Develop => "展开：在已有内容上增加新的具体信息",
            ConversationBeat.Contrast => "对照：表达与已有观点不同的一点",
            ConversationBeat.Clarify => "澄清：回答或弄清尚未解决的具体问题",
            ConversationBeat.Resolve => "落定：形成决定或收拢仍未解决的内容",
            ConversationBeat.Close => "收束：自然结束，不开启新分支",
            _ => throw new ArgumentOutOfRangeException(nameof(beat))
        };

    private static string GetLengthInstruction(ConversationMessageLength length) =>
        length switch
        {
            ConversationMessageLength.VeryShort => "保持简短，通常使用一个短句或短语；但仍须完成当前动作的核心内容",
            ConversationMessageLength.Short => "使用自然的一到三句完成当前回应，不必解释无关背景",
            ConversationMessageLength.Moderate => "可以适当展开并自然拆成多条，把当前问题说明白，但不要形成文章结构",
            _ => throw new ArgumentOutOfRangeException(nameof(length))
        };

    private static string GetDirectnessInstruction(ConversationDirectness directness) =>
        directness switch
        {
            ConversationDirectness.Direct => "直接回应最核心的一点",
            ConversationDirectness.Partial => "只接其中一个点，其他内容可以暂时不回应",
            ConversationDirectness.Indirect => "允许绕开或侧面回应，不必给出明确结论",
            _ => throw new ArgumentOutOfRangeException(nameof(directness))
        };

    private static string GetQuestionInstruction(ConversationQuestionMode questionMode) =>
        questionMode switch
        {
            ConversationQuestionMode.None => "不要提问",
            ConversationQuestionMode.Optional => "只有非常自然时才带一个小问题，不必强行提问",
            ConversationQuestionMode.Natural => "提出一个自然、具体的问题，不要连续追问",
            _ => throw new ArgumentOutOfRangeException(nameof(questionMode))
        };

    private static string GetEmotionInstruction(ConversationEmotionVisibility emotionVisibility) =>
        emotionVisibility switch
        {
            ConversationEmotionVisibility.Restrained => "情绪收着表达，不使用夸张感叹",
            ConversationEmotionVisibility.Natural => "自然显露一点情绪，不刻意强调",
            ConversationEmotionVisibility.Open => "可以明显表达即时情绪，但不要表演化",
            _ => throw new ArgumentOutOfRangeException(nameof(emotionVisibility))
        };

    private static string GetTopicInstruction(ConversationTopicMovement topicMovement) =>
        topicMovement switch
        {
            ConversationTopicMovement.Stay => "停留在当前话题的一个小点",
            ConversationTopicMovement.SlightDrift => "可以自然联想到相邻内容，但不要完全跑题",
            ConversationTopicMovement.Shift => "转向一个相邻话题，不要宣布自己正在转移话题",
            _ => throw new ArgumentOutOfRangeException(nameof(topicMovement))
        };

    private static string GetPunctuationInstruction(ConversationPunctuationRhythm punctuationRhythm) =>
        punctuationRhythm switch
        {
            ConversationPunctuationRhythm.Sparse => "标点克制，句式简单",
            ConversationPunctuationRhythm.Natural => "使用自然聊天标点，不必每句都写成规范书面句",
            ConversationPunctuationRhythm.Expressive => "标点可以体现情绪，但不要连续堆叠感叹号或省略号",
            _ => throw new ArgumentOutOfRangeException(nameof(punctuationRhythm))
        };

    private static string GetRelationshipInstruction(ConversationRelationshipTone relationshipTone) =>
        relationshipTone switch
        {
            ConversationRelationshipTone.Unknown => "没有可靠关系资料，保持自然分寸，不凭空表现得过分亲密",
            ConversationRelationshipTone.Distant => "关系疏远，表达克制，保留明显边界",
            ConversationRelationshipTone.Reserved => "还不算熟，语气友好但有所保留",
            ConversationRelationshipTone.Familiar => "已经熟悉，可以放松、省略和自然接话",
            ConversationRelationshipTone.Close => "关系亲近，可以坦率、默契或轻微调侃",
            _ => throw new ArgumentOutOfRangeException(nameof(relationshipTone))
        };

    private static string GetRelationshipBalanceInstruction(
        ConversationRelationshipBalance relationshipBalance) =>
        relationshipBalance switch
        {
            ConversationRelationshipBalance.Unknown => "没有足够信息判断双方投入是否对等",
            ConversationRelationshipBalance.Balanced => "双方关系感受接近，不必刻意试探",
            ConversationRelationshipBalance.SpeakerMoreInvested => "你更在意对方，表达可以主动一些，但不要假定对方同样亲近",
            ConversationRelationshipBalance.OtherMoreInvested => "对方显得更主动，你可以接受这种亲近，也可以按自身性格保留距离",
            _ => throw new ArgumentOutOfRangeException(nameof(relationshipBalance))
        };

    private static string JoinTags(AiAccount account, AiAccountTagType type)
    {
        string joined = string.Join(
            "、",
            account.Tags.Where(tag => tag.Type == type).Select(tag => tag.Value));
        return DisplayOrDefault(joined);
    }

    private static string DisplayOrDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "未填写" : value.Trim();

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : $"{value[..maximumLength]}…";

    private static string NormalizeForComparison(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static bool IsSemanticallyNearDuplicate(
        string generatedMessage,
        string recentMessage)
    {
        string generated = NormalizeForComparison(generatedMessage);
        string recent = NormalizeForComparison(recentMessage);
        int shorterLength = Math.Min(generated.Length, recent.Length);

        if (shorterLength < 8)
        {
            return false;
        }

        if (generated.Contains(recent, StringComparison.Ordinal)
            || recent.Contains(generated, StringComparison.Ordinal))
        {
            return true;
        }

        HashSet<string> generatedBigrams = CreateCharacterNGrams(
            generated,
            2);
        HashSet<string> recentBigrams = CreateCharacterNGrams(recent, 2);
        int sharedCount = generatedBigrams.Intersect(recentBigrams).Count();
        double diceSimilarity = 2d * sharedCount
            / (generatedBigrams.Count + recentBigrams.Count);

        return diceSimilarity >= 0.68;
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

    private static bool HasUnsupportedFirstPersonExperience(
        string generatedMessage,
        AiMessageGenerationRequest request)
    {
        string[] explicitOtherSubjectMarkers =
        {
            "你上次", "上次你", "你之前", "之前你", "你以前", "以前你",
            "他上次", "她上次", "他们上次", "她们上次", "听你说"
        };
        string[] temporalMarkers =
        {
            "上次", "之前", "以前", "昨晚", "昨天", "前天", "当时",
            "那次", "去年", "小时候", "最近", "这阵子", "目前",
            "正在", "刚完成", "刚接到", "准备", "计划", "明天"
        };
        string[] experienceVerbs =
        {
            "去", "看", "做", "买", "遇", "参加", "住", "吃", "玩",
            "画", "写", "养", "工作", "旅行", "演出"
        };
        string[] explicitFirstPersonExperienceMarkers =
        {
            "我去过", "我看过", "我做过", "我买过", "我遇到过",
            "我参加过", "我住过", "我养过", "我经历过", "我曾经",
            "我最近", "我这阵子", "我目前", "我正在", "我刚完成",
            "我刚接到", "我准备", "我计划", "我明天"
        };

        bool explicitlyAboutOther = explicitOtherSubjectMarkers.Any(marker =>
            generatedMessage.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase));
        bool hasExplicitFirstPersonExperience =
            explicitFirstPersonExperienceMarkers.Any(marker =>
                generatedMessage.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase));
        bool hasOmittedSubjectPastExperience = !explicitlyAboutOther
            && temporalMarkers.Any(marker => generatedMessage.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase))
            && experienceVerbs.Any(verb => generatedMessage.Contains(
                verb,
                StringComparison.OrdinalIgnoreCase));

        if (!hasExplicitFirstPersonExperience
            && !hasOmittedSubjectPastExperience)
        {
            return false;
        }

        List<string> ownGrounding = request.RecentMessages
            .Where(message =>
                message.SenderType == MessageSenderType.AiAccount
                && message.SenderAiAccountId == request.Speaker.Id)
            .Select(message => message.Content)
            .ToList();
        ownGrounding.AddRange(GetProfileGrounding(request.Speaker));
        ownGrounding.AddRange(request.RelevantSelfMemories
            .Where(memory => memory.AiAccountId == request.Speaker.Id)
            .Select(memory => memory.Summary));
        ownGrounding.AddRange(request.DirectionPlan?.SelfMemoryProposals
            .Select(proposal => proposal.Summary)
            ?? Array.Empty<string>());
        ownGrounding.AddRange(GetValidSharedMemorySummaries(request));

        return !ownGrounding.Any(content =>
            HasGroundingOverlap(generatedMessage, content));
    }

    private static bool ClaimsUnsupportedSharedConclusion(string message)
    {
        string[] unsupportedMarkers =
        {
            "咱俩都", "我们都", "看来你也", "原来你也", "你也不想",
            "你也想", "你也觉得", "你也同意", "你也决定", "我们一致",
            "咱们一致", "那就这么定"
        };

        return unsupportedMarkers.Any(marker => message.Contains(
            marker,
            StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasGroundingOverlap(
        string generatedMessage,
        string source)
    {
        return AiFactGroundingMatcher.HasGroundingOverlap(
            generatedMessage,
            source);
    }

    private static bool HasLikelyBorrowedFirstPersonExperience(
        string generatedMessage,
        AiMessageGenerationRequest request,
        IReadOnlyList<string> otherParticipantContents)
    {
        string[] firstPersonExperienceMarkers =
        {
            "我昨晚", "我昨天", "我之前", "我以前", "我当时",
            "我也去", "我也看", "我也做", "我也有", "我也遇到"
        };
        if (!firstPersonExperienceMarkers.Any(marker =>
                generatedMessage.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (GetValidOwnFactSummaries(request).Any(summary =>
                HasGroundingOverlap(generatedMessage, summary)))
        {
            return false;
        }

        string normalizedGeneratedMessage = NormalizeForComparison(
            generatedMessage);
        return otherParticipantContents.Any(content =>
            GetDistinctiveFragments(content).Any(fragment =>
                normalizedGeneratedMessage.Contains(
                    fragment,
                    StringComparison.Ordinal)));
    }

    private static IEnumerable<string> GetValidSharedMemorySummaries(
        AiMessageGenerationRequest request)
    {
        HashSet<Guid> participantIds = request.OtherParticipants
            .Select(participant => participant.Id)
            .ToHashSet();
        return request.RelevantMemories
            .Where(memory =>
                memory.OwnerAiAccountId == request.Speaker.Id
                && participantIds.Contains(memory.SubjectAiAccountId)
                && memory.Type == AiMemoryType.SharedExperience)
            .Select(memory => memory.Summary);
    }

    private static IEnumerable<string> GetValidOwnFactSummaries(
        AiMessageGenerationRequest request)
    {
        return GetProfileGrounding(request.Speaker)
            .Concat(request.RelevantSelfMemories
                .Where(memory => memory.AiAccountId == request.Speaker.Id)
                .Select(memory => memory.Summary))
            .Concat(request.DirectionPlan?.SelfMemoryProposals
                .Select(proposal => proposal.Summary)
                ?? Array.Empty<string>())
            .Concat(GetValidSharedMemorySummaries(request));
    }

    private static IEnumerable<string> GetProfileGrounding(AiAccount account)
    {
        return new[]
        {
            account.IdentityDescription,
            account.Personality,
            account.SpeakingStyle,
            account.Signature,
            account.Location,
            account.Occupation,
            account.Hometown
        }.Where(value => !string.IsNullOrWhiteSpace(value));
    }

    private static IEnumerable<string> GetDistinctiveFragments(string value)
    {
        const int fragmentLength = 4;
        string normalizedValue = NormalizeForComparison(value);

        for (int index = 0;
             index <= normalizedValue.Length - fragmentLength;
             index++)
        {
            yield return normalizedValue.Substring(index, fragmentLength);
        }
    }

    private static string BuildJsonExample(int messageCount)
    {
        string messages = string.Join(
            ',',
            Enumerable.Range(1, messageCount).Select(index => $"\"消息{index}\""));
        return $"{{\"messages\":[{messages}]}}";
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
}

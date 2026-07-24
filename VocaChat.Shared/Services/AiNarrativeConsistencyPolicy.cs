using VocaChat.Models;
using System.Text.RegularExpressions;

namespace VocaChat.Services;

/// <summary>
/// 区分可以通过有限重试修正的表达问题和绝不能保存的事实边界问题。
/// </summary>
internal enum AiNarrativeConsistencySeverity
{
    None,
    Soft,
    Hard
}

/// <summary>
/// 表示一次角色世界叙事一致性检查的结果。
/// </summary>
internal sealed record AiNarrativeConsistencyDecision(
    AiNarrativeConsistencySeverity Severity,
    string Reason)
{
    public bool RequiresRegeneration =>
        Severity != AiNarrativeConsistencySeverity.None;

    public static AiNarrativeConsistencyDecision Allowed { get; } =
        new(AiNarrativeConsistencySeverity.None, string.Empty);
}

/// <summary>
/// 按角色所属世界检查可见消息中的叙事一致性。
/// 新人物、地点和名词可以自然进入叙事；只有占位名称以及现实世界中
/// 没有可靠来源的时效性外部状态需要重新生成。
/// </summary>
internal static class AiNarrativeConsistencyPolicy
{
    private static readonly string[] ExternalVenueCues =
    {
        "推荐", "可以去", "去看看", "那家", "这家", "店", "馆",
        "现场", "演出", "营业", "地址", "老板", "店主", "门票"
    };

    private static readonly string[] ExternalStatusMarkers =
    {
        "通宵开放", "24小时营业", "二十四小时营业", "营业到",
        "开馆时间", "闭馆时间", "正在举办", "正在展出", "近期有场",
        "最近有场", "今晚有场", "本周有场", "这周有场", "近期演出",
        "最近演出", "门票价格", "票价是", "地址在", "老板收",
        "店主收", "老板是", "店主是"
    };

    private static readonly string[] ConditionalMarkers =
    {
        "如果", "假如", "可以找", "可以选", "可以考虑", "不妨",
        "或许", "可能", "先确认", "可以问问", "要是", "有机会",
        "希望", "想象", "真能", "没法", "不能", "只能", "隔着",
        "远程", "通信"
    };

    private static readonly string[] CrossWorldPhysicalMarkers =
    {
        "一起去", "一起到", "一起见", "见个面", "见面", "碰面",
        "你来我这", "你来这里", "我去找你", "我到你那", "去你那",
        "来找我", "带你去", "带你看看", "给你送", "递给你",
        "坐在一起", "住在一起", "当面聊", "当面说"
    };

    private static readonly string[] FirstPersonWorldExperienceMarkers =
    {
        "我在", "我从", "我去", "我来过", "我到过", "我见过",
        "我住在", "我生活在", "我经历过", "我穿过", "我带着",
        "我骑着", "我那边", "我这边", "我们那边", "我们的世界",
        "那次"
    };

    /// <summary>
    /// 检查一批待发送消息；允许新增叙事名词，只拦截明确违反当前世界
    /// 事实边界的内容。
    /// </summary>
    public static AiNarrativeConsistencyDecision Evaluate(
        IReadOnlyList<string> messages,
        AiMessageGenerationRequest request,
        AiWorldConversationContext? worldConversationContext = null,
        AiGroupWorldConversationContext? groupWorldConversationContext = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<string> trustedSources = GetTrustedSources(request);
        AiNarrativeConsistencyDecision protectedRecallDecision =
            EvaluateExplicitProtectedFactRecall(messages, request);
        if (protectedRecallDecision.RequiresRegeneration)
        {
            return protectedRecallDecision;
        }

        foreach (string message in messages)
        {
            if (ContainsPlaceholderVenue(message))
            {
                return new AiNarrativeConsistencyDecision(
                    AiNarrativeConsistencySeverity.Soft,
                    "消息包含“XX”或“某某”一类外部场所占位名称。"
                    + "请改成自然、明确的名称或场所类型。");
            }

            AiNarrativeConsistencyDecision protectedFactDecision =
                EvaluateProtectedSelfFactBoundary(message, request);
            if (protectedFactDecision.RequiresRegeneration)
            {
                return protectedFactDecision;
            }

            AiNarrativeConsistencyDecision protectedCausalDecision =
                EvaluateProtectedCausalDirection(message, request);
            if (protectedCausalDecision.RequiresRegeneration)
            {
                return protectedCausalDecision;
            }

            AiNarrativeConsistencyDecision crossWorldDecision =
                EvaluateCrossWorldBoundary(
                    message,
                    request,
                    worldConversationContext,
                    groupWorldConversationContext);
            if (crossWorldDecision.RequiresRegeneration)
            {
                return crossWorldDecision;
            }

            if (RequiresReliableExternalStatusSource(
                    request.Speaker.CharacterWorldId)
                && ContainsDefinitiveExternalStatus(message)
                && !trustedSources.Any(source =>
                    SupportsExternalStatus(message, source)))
            {
                return new AiNarrativeConsistencyDecision(
                    AiNarrativeConsistencySeverity.Hard,
                    "消息把没有可靠来源的现实世界营业、活动或经营信息"
                    + "写成了确定事实。请删除该状态，改用条件式表达或"
                    + "提醒对方自行确认。");
            }
        }

        return AiNarrativeConsistencyDecision.Allowed;
    }

    /// <summary>
    /// 受保护事实用“因此/所以”明确关系时，禁止回复把结果中的概念
    /// 反过来写成前件的原因。
    /// </summary>
    private static AiNarrativeConsistencyDecision
        EvaluateProtectedCausalDirection(
        string message,
        AiMessageGenerationRequest request)
    {
        string[] protectedConnectors = { "因此", "所以", "因而" };
        string[] generatedCausalMarkers = { "引发", "导致", "造成" };

        foreach (AiConversationSelfMemory memory in request
                     .RelevantSelfMemories
                     .Where(candidate =>
                         candidate.AiAccountId == request.Speaker.Id
                         && candidate.IsProtectedFact))
        {
            int protectedConnectorIndex = -1;
            string? protectedConnector = null;
            foreach (string connector in protectedConnectors)
            {
                protectedConnectorIndex = memory.Summary.IndexOf(
                    connector,
                    StringComparison.Ordinal);
                if (protectedConnectorIndex >= 0)
                {
                    protectedConnector = connector;
                    break;
                }
            }

            if (protectedConnector is null)
            {
                continue;
            }

            string premise = memory.Summary[..protectedConnectorIndex];
            string consequence = memory.Summary[
                (protectedConnectorIndex + protectedConnector.Length)..];
            foreach (string marker in generatedCausalMarkers)
            {
                int markerIndex = message.IndexOf(
                    marker,
                    StringComparison.Ordinal);
                if (markerIndex <= 0)
                {
                    continue;
                }

                string generatedCause = message[..markerIndex];
                string generatedResult =
                    message[(markerIndex + marker.Length)..];
                if (AiFactGroundingMatcher.HasTopicOverlap(
                        generatedCause,
                        consequence)
                    && AiFactGroundingMatcher.HasTopicOverlap(
                        generatedResult,
                        premise))
                {
                    return new AiNarrativeConsistencyDecision(
                        AiNarrativeConsistencySeverity.Hard,
                        "回复倒置了受保护事实已经明确的因果方向。"
                        + "必须保持原条目中的前因后果，不能把结果反写成原因。");
                }
            }
        }

        return AiNarrativeConsistencyDecision.Allowed;
    }

    /// <summary>
    /// 只有用户明确要求按已确认事实回忆或纠正时，才要求回复实际覆盖
    /// 对应受保护事实，避免模型用安全套话回避已有的确定信息。
    /// </summary>
    private static AiNarrativeConsistencyDecision
        EvaluateExplicitProtectedFactRecall(
        IReadOnlyList<string> messages,
        AiMessageGenerationRequest request)
    {
        string currentTarget = string.Join(
            ' ',
            new[]
            {
                request.FocusContent,
                request.ReplyTarget?.Message?.Content ?? string.Empty,
                request.ConversationAnchor?.Content ?? string.Empty
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        string[] explicitRecallMarkers =
        {
            "确认的事实", "确定的事实", "确定的记忆", "按你确定",
            "按你的记忆", "请纠正", "纠正我", "你自己记得",
            "到底是"
        };
        if (!explicitRecallMarkers.Any(marker => currentTarget.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase)))
        {
            return AiNarrativeConsistencyDecision.Allowed;
        }

        IReadOnlyList<AiConversationSelfMemory> relevantProtectedFacts =
            request.RelevantSelfMemories
                .Where(memory =>
                    memory.AiAccountId == request.Speaker.Id
                    && memory.IsProtectedFact
                    && AiFactGroundingMatcher.HasTopicOverlap(
                        currentTarget,
                        memory.Summary))
                .ToList()
                .AsReadOnly();
        if (relevantProtectedFacts.Count == 0)
        {
            return AiNarrativeConsistencyDecision.Allowed;
        }

        string combinedReply = string.Join(' ', messages);
        if (relevantProtectedFacts.Any(memory =>
                AiFactGroundingMatcher.HasGroundingOverlap(
                    combinedReply,
                    memory.Summary)))
        {
            return AiNarrativeConsistencyDecision.Allowed;
        }

        return new AiNarrativeConsistencyDecision(
            AiNarrativeConsistencySeverity.Hard,
            "对方明确要求按已确认事实回答，但回复没有覆盖相关受保护事实。"
            + "请直接说明受保护事实，不要使用含糊的安全套话回避。");
    }

    /// <summary>
    /// 当其他参与者对当前角色的恒定事实提出冲突版本时，禁止回复直接
    /// 采用冲突细节。对话文本仍可被纠正、质疑或作为他人说法引用。
    /// </summary>
    private static AiNarrativeConsistencyDecision
        EvaluateProtectedSelfFactBoundary(
        string message,
        AiMessageGenerationRequest request)
    {
        IReadOnlyList<AiConversationSelfMemory> protectedFacts = request
            .RelevantSelfMemories
            .Where(memory =>
                memory.AiAccountId == request.Speaker.Id
                && memory.IsProtectedFact)
            .ToList()
            .AsReadOnly();
        if (protectedFacts.Count == 0
            || PreservesUncertaintyOrRefutes(message))
        {
            return AiNarrativeConsistencyDecision.Allowed;
        }

        IReadOnlyList<string> otherParticipantClaims = request.RecentMessages
            .Where(recent =>
                recent.SenderType == MessageSenderType.User
                || recent.SenderAiAccountId != request.Speaker.Id)
            .Select(recent => recent.Content)
            .Concat(request.ReplyTarget?.Message is null
                ? Array.Empty<string>()
                : new[] { request.ReplyTarget.Message.Content })
            .Concat(request.ConversationAnchor is null
                ? Array.Empty<string>()
                : new[] { request.ConversationAnchor.Content })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();

        foreach (AiConversationSelfMemory protectedFact in protectedFacts)
        {
            foreach (string claim in otherParticipantClaims)
            {
                if (!AiFactGroundingMatcher.HasGroundingOverlap(
                        claim,
                        protectedFact.Summary))
                {
                    continue;
                }

                IReadOnlyList<string> conflictingFragments =
                    GetDistinctiveConflictFragments(
                        claim,
                        protectedFact.Summary);
                if (!conflictingFragments.Any(fragment =>
                        NormalizeFactText(message).Contains(
                            fragment,
                            StringComparison.Ordinal)))
                {
                    continue;
                }

                return new AiNarrativeConsistencyDecision(
                    AiNarrativeConsistencySeverity.Hard,
                    "回复采用了其他参与者对当前好友受保护事实提出的"
                    + "冲突细节。受保护事实不能被近期说法覆盖；"
                    + "请保持原事实，或自然纠正、质疑和保留不确定性。");
            }
        }

        return AiNarrativeConsistencyDecision.Allowed;
    }

    private static IReadOnlyList<string> GetDistinctiveConflictFragments(
        string claim,
        string protectedFact)
    {
        string normalizedClaim = NormalizeFactText(claim);
        string normalizedProtected = NormalizeFactText(protectedFact);
        int fragmentLength = normalizedClaim.Any(character => character > 127)
            ? 4
            : 5;
        if (normalizedClaim.Length < fragmentLength)
        {
            return Array.Empty<string>();
        }

        HashSet<string> fragments = new(StringComparer.Ordinal);
        for (int index = 0;
             index <= normalizedClaim.Length - fragmentLength;
             index++)
        {
            string fragment = normalizedClaim.Substring(
                index,
                fragmentLength);
            if (!normalizedProtected.Contains(
                    fragment,
                    StringComparison.Ordinal))
            {
                fragments.Add(fragment);
            }
        }

        return fragments.ToList().AsReadOnly();
    }

    private static bool PreservesUncertaintyOrRefutes(string message)
    {
        string[] markers =
        {
            "不是", "并不是", "并非", "不对", "你记错", "没有这回事",
            "我不记得", "不能确认", "不确定", "你说的是", "你是指",
            "为什么会", "怎么会", "依据是什么"
        };
        return markers.Any(marker => message.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase))
            || (message.Contains('?') || message.Contains('？'))
                && message.Contains('你');
    }

    private static string NormalizeFactText(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    /// <summary>
    /// 不禁止跨世界交流或讨论新名词，只禁止在没有明确规则时把通信
    /// 自动写成物理共处，或把其他世界写成当前角色的亲历地点。
    /// </summary>
    private static AiNarrativeConsistencyDecision EvaluateCrossWorldBoundary(
        string message,
        AiMessageGenerationRequest request,
        AiWorldConversationContext? worldConversationContext,
        AiGroupWorldConversationContext? groupWorldConversationContext)
    {
        IReadOnlyList<AiAccount> crossWorldParticipants =
            request.OtherParticipants
                .Where(account =>
                    account.Id != request.Speaker.Id
                    && account.CharacterWorldId !=
                        request.Speaker.CharacterWorldId)
                .ToList()
                .AsReadOnly();
        if (crossWorldParticipants.Count == 0)
        {
            return AiNarrativeConsistencyDecision.Allowed;
        }

        if (groupWorldConversationContext is not null)
        {
            AiWorldConversationContext? activeTargetContext =
                request.RelationshipTarget is null
                    ? null
                    : groupWorldConversationContext.FindParticipant(
                        request.RelationshipTarget.Id);
            bool canConcludeCrossWorld = activeTargetContext is not null
                ? activeTargetContext.RelationshipAwareness ==
                    AiWorldAwarenessState.CrossWorldConfirmed
                : groupWorldConversationContext.ParticipantContexts.Any(
                    context =>
                        context.RelationshipAwareness ==
                            AiWorldAwarenessState.CrossWorldConfirmed);
            if (!canConcludeCrossWorld
                && ContainsDirectCrossWorldConclusion(message))
            {
                return new AiNarrativeConsistencyDecision(
                    AiNarrativeConsistencySeverity.Hard,
                    "当前发言者尚未通过群聊或私聊证据确认任何相关成员"
                    + "来自不同世界，不能提前宣布跨世界关系。");
            }

            foreach (AiAccount participant in crossWorldParticipants)
            {
                AiWorldConversationContext? participantContext =
                    groupWorldConversationContext.FindParticipant(
                        participant.Id);
                AiNarrativeConsistencyDecision knowledgeDecision =
                    EvaluateWorldKnowledgeBoundary(
                        message,
                        request,
                        participant,
                        participantContext,
                        validateCrossWorldConclusion: false);
                if (knowledgeDecision.RequiresRegeneration)
                {
                    return knowledgeDecision;
                }
            }
        }
        else if (worldConversationContext is not null
                 && request.RelationshipTarget is not null)
        {
            AiNarrativeConsistencyDecision knowledgeDecision =
                EvaluateWorldKnowledgeBoundary(
                    message,
                    request,
                    request.RelationshipTarget,
                    worldConversationContext,
                    validateCrossWorldConclusion: true);
            if (knowledgeDecision.RequiresRegeneration)
            {
                return knowledgeDecision;
            }
        }

        if (ContainsConditionalOrRemoteBoundary(message))
        {
            return AiNarrativeConsistencyDecision.Allowed;
        }

        bool targetsCrossWorldRelationship =
            request.RelationshipTarget is not null
            && crossWorldParticipants.Any(account =>
                account.Id == request.RelationshipTarget.Id);
        bool hasCollectivePhysicalClaim =
            CrossWorldPhysicalMarkers.Any(marker => message.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase));

        if (hasCollectivePhysicalClaim
            && (targetsCrossWorldRelationship
                || request.RelationshipTarget is null))
        {
            return new AiNarrativeConsistencyDecision(
                AiNarrativeConsistencySeverity.Hard,
                "当前参与者属于不同角色世界，本次只能按远程通信理解。"
                + "不能无依据声称已经见面、共同到访或传递物品；"
                + "可以改成听闻、假设或明确的远程交流。");
        }

        bool hasFirstPersonExperience =
            FirstPersonWorldExperienceMarkers.Any(marker => message.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase));
        if (!hasFirstPersonExperience)
        {
            return AiNarrativeConsistencyDecision.Allowed;
        }

        foreach (AiAccount participant in crossWorldParticipants)
        {
            IEnumerable<string> foreignWorldSources =
                GetForeignWorldSources(participant);
            if (foreignWorldSources.Any(source =>
                    AiFactGroundingMatcher.HasGroundingOverlap(
                        message,
                        source)))
            {
                return new AiNarrativeConsistencyDecision(
                    AiNarrativeConsistencySeverity.Hard,
                    $"消息把属于“{participant.Nickname}”角色世界的内容"
                    + "写成了当前发言者的亲身经历。应保留听闻距离，"
                    + "不能改变事实主体。");
            }
        }

        return AiNarrativeConsistencyDecision.Allowed;
    }

    /// <summary>
    /// 防止模型利用预训练知识或完整账号资料，越过当前发言者已经形成的
    /// 方向性世界认知。
    /// </summary>
    private static AiNarrativeConsistencyDecision
        EvaluateWorldKnowledgeBoundary(
            string message,
            AiMessageGenerationRequest request,
            AiAccount subject,
            AiWorldConversationContext? context,
            bool validateCrossWorldConclusion)
    {
        if (validateCrossWorldConclusion
            && context?.RelationshipAwareness !=
                AiWorldAwarenessState.CrossWorldConfirmed
            && ContainsDirectCrossWorldConclusion(message))
        {
            return new AiNarrativeConsistencyDecision(
                AiNarrativeConsistencySeverity.Hard,
                "当前发言者尚未通过对话确认双方来自不同世界，"
                + "不能提前宣布跨世界关系。应先按普通环境差异理解，"
                + "或只询问当前陌生概念。");
        }

        IReadOnlyList<string> authorizedSources =
            GetAuthorizedForeignKnowledgeSources(request, context);
        foreach (string term in GetProtectedForeignTerms(subject))
        {
            if (!message.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase)
                || authorizedSources.Any(source => source.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            return new AiNarrativeConsistencyDecision(
                AiNarrativeConsistencySeverity.Hard,
                $"当前发言者尚未从对话中获知“{term}”，"
                + "不能使用模型先验或对方完整资料补充其他世界知识。");
        }

        return AiNarrativeConsistencyDecision.Allowed;
    }

    private static bool ContainsDirectCrossWorldConclusion(string message)
    {
        string[] markers =
        {
            "你来自另一个世界",
            "你是另一个世界的人",
            "我们来自不同世界",
            "我们不在同一个世界",
            "我们不是一个世界的",
            "原来是跨世界",
            "这是跨世界通信",
            "正在跨世界通信"
        };
        return markers.Any(marker => message.Contains(
            marker,
            StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string>
        GetAuthorizedForeignKnowledgeSources(
            AiMessageGenerationRequest request,
            AiWorldConversationContext? context)
    {
        List<string> sources = request.RecentMessages
            .Select(item => item.Content)
            .ToList();
        if (request.ReplyTarget?.Message is not null)
        {
            sources.Add(request.ReplyTarget.Message.Content);
        }

        if (request.ConversationAnchor is not null)
        {
            sources.Add(request.ConversationAnchor.Content);
        }

        if (context is not null)
        {
            sources.AddRange(context.RelevantKnowledge
                .Select(item => item.Summary));
            if (context.CanNameSubjectWorld)
            {
                sources.Add(context.VisibleSubjectWorldName!);
            }
        }

        return sources
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<string> GetProtectedForeignTerms(
        AiAccount subject)
    {
        HashSet<string> terms =
            new(StringComparer.OrdinalIgnoreCase);
        AddProtectedTerm(terms, subject.CharacterWorld?.Name);
        AddProtectedTerm(terms, subject.Location);
        AddProtectedTerm(terms, subject.Hometown);

        foreach (string? source in new[]
                 {
                     subject.CharacterWorld?.Description,
                     subject.IdentityDescription,
                     subject.Location,
                     subject.Hometown,
                     subject.Occupation
                 }.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            AddNamedEntityTerms(terms, source!);
        }

        return terms
            .OrderByDescending(term => term.Length)
            .ToList()
            .AsReadOnly();
    }

    private static void AddProtectedTerm(
        ISet<string> terms,
        string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length >= 2
            && !IsGenericWorldTerm(normalized))
        {
            terms.Add(normalized);
        }
    }

    private static void AddNamedEntityTerms(
        ISet<string> terms,
        string source)
    {
        const string suffixPattern =
            "学院|学园|学校|高中|大学|城市|地区|大陆|星球|帝国|王国|"
            + "联邦|共和国|军团|舰队|组织|教会|公司|自治区";
        foreach (Match match in Regex.Matches(
                     source,
                     $@"[\p{{IsCJKUnifiedIdeographs}}]{{2,12}}(?:{suffixPattern})"))
        {
            string value = match.Value;
            string suffix = Regex.Match(
                value,
                $"(?:{suffixPattern})$").Value;
            string stem = value[..^suffix.Length];
            for (int length = 2;
                 length <= Math.Min(8, stem.Length);
                 length++)
            {
                AddProtectedTerm(
                    terms,
                    stem[^length..]);
            }
        }

        foreach (Match match in Regex.Matches(
                     source,
                     @"\b[A-Z][A-Za-z0-9_-]{3,}\b"))
        {
            AddProtectedTerm(terms, match.Value);
        }
    }

    private static bool IsGenericWorldTerm(string value)
    {
        string[] ignored =
        {
            "现实世界", "另一个世界", "其他世界", "平行世界",
            "学校", "学院", "城市", "地区", "组织", "公司",
            "朋友", "学生", "老师"
        };
        return ignored.Contains(
            value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 默认现实世界继续要求时效性外部状态具备可靠来源；用户创建的
    /// 角色世界允许其内部叙事自然发展。
    /// </summary>
    public static bool RequiresReliableExternalStatusSource(
        Guid characterWorldId) =>
        characterWorldId == CharacterWorld.DefaultWorldId;

    /// <summary>
    /// 识别需要现实来源支持的营业、活动和经营状态。
    /// </summary>
    public static bool ContainsDefinitiveExternalStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || ConditionalMarkers.Any(marker => value.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return ExternalStatusMarkers.Any(marker => value.Contains(
            marker,
            StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> GetTrustedSources(
        AiMessageGenerationRequest request)
    {
        List<string> sources = GetProfileSources(request.Speaker).ToList();
        sources.AddRange(request.RelevantSelfMemories
            .Where(memory =>
                memory.AiAccountId == request.Speaker.Id
                && memory.Source == AiSelfMemorySource.User)
            .Select(memory => memory.Summary));
        sources.AddRange(request.RecentMessages
            .Where(message => message.SenderType == MessageSenderType.User)
            .Select(message => message.Content));

        if (request.ConversationAnchor?.SenderType == MessageSenderType.User)
        {
            sources.Add(request.ConversationAnchor.Content);
        }

        if (request.ReplyTarget?.Message?.SenderType ==
            MessageSenderType.User)
        {
            sources.Add(request.ReplyTarget.Message.Content);
        }

        return sources
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static IEnumerable<string> GetProfileSources(AiAccount account)
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

    private static IEnumerable<string> GetForeignWorldSources(
        AiAccount account)
    {
        if (account.CharacterWorld is not null)
        {
            yield return account.CharacterWorld.Name;
            if (!string.IsNullOrWhiteSpace(account.CharacterWorld.Description))
            {
                yield return account.CharacterWorld.Description;
            }
        }

        foreach (string source in GetProfileSources(account))
        {
            yield return source;
        }
    }

    private static bool ContainsConditionalOrRemoteBoundary(string message) =>
        ConditionalMarkers.Any(marker => message.Contains(
            marker,
            StringComparison.OrdinalIgnoreCase));

    private static bool SupportsExternalStatus(
        string message,
        string source)
    {
        bool sharesStatusMarker = ExternalStatusMarkers.Any(marker =>
            message.Contains(marker, StringComparison.OrdinalIgnoreCase)
            && source.Contains(marker, StringComparison.OrdinalIgnoreCase));
        return sharesStatusMarker
            && AiFactGroundingMatcher.HasGroundingOverlap(message, source);
    }

    private static bool ContainsPlaceholderVenue(string message)
    {
        bool hasPlaceholder = message.Contains(
                "XX",
                StringComparison.OrdinalIgnoreCase)
            || message.Contains("某某", StringComparison.OrdinalIgnoreCase);
        return hasPlaceholder
            && ExternalVenueCues.Any(cue => message.Contains(
                cue,
                StringComparison.OrdinalIgnoreCase));
    }
}

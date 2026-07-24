using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 区分一条历史消息相对于当前发言账号的事实归属。
/// </summary>
public enum AiConversationMessageOwnership
{
    CurrentSpeaker,
    ReplyTargetAiAccount,
    OtherAiAccount,
    LocalUser
}

/// <summary>
/// 限定一条上下文消息可以怎样参与当前发言者的事实表达。
/// </summary>
public enum AiConversationFactUsage
{
    SpeakerNarrative,
    HearsayOnly,
    UserProvidedContext
}

/// <summary>
/// 表示一条已经完成身份归属判定的生成上下文消息。
/// </summary>
public sealed record AiConversationContextMessage(
    AiDialogueMessage Message,
    AiConversationMessageOwnership Ownership,
    Guid? CharacterWorldId = null)
{
    public Guid? FactOwnerAiAccountId => Message.SenderAiAccountId;

    public AiConversationFactUsage FactUsage => Ownership switch
    {
        AiConversationMessageOwnership.CurrentSpeaker =>
            AiConversationFactUsage.SpeakerNarrative,
        AiConversationMessageOwnership.ReplyTargetAiAccount or
            AiConversationMessageOwnership.OtherAiAccount =>
            AiConversationFactUsage.HearsayOnly,
        AiConversationMessageOwnership.LocalUser =>
            AiConversationFactUsage.UserProvidedContext,
        _ => throw new ArgumentOutOfRangeException(nameof(Ownership))
    };
}

/// <summary>
/// 保存按照原始时间顺序排列、但具有明确身份归属的最近消息。
/// </summary>
public sealed class AiConversationContext
{
    public AiConversationContextMessage? ReplyTarget { get; }
    public IReadOnlyList<AiConversationContextMessage> Messages { get; }
    public IReadOnlyList<AiConversationMemory> Memories { get; }
    public IReadOnlyList<AiConversationSelfMemory> SelfMemories { get; }
    public AiWorldConversationContext? WorldConversationContext { get; }
    public AiGroupWorldConversationContext? GroupWorldConversationContext
        { get; }
    public IReadOnlyList<Guid> CrossWorldAiAccountIds { get; }
    public TimeSpan? GapSincePreviousMessage { get; }

    internal AiConversationContext(
        AiConversationContextMessage? replyTarget,
        IReadOnlyList<AiConversationContextMessage> messages,
        IReadOnlyList<AiConversationMemory> memories,
        IReadOnlyList<AiConversationSelfMemory> selfMemories,
        AiWorldConversationContext? worldConversationContext,
        AiGroupWorldConversationContext? groupWorldConversationContext,
        IReadOnlyList<Guid> crossWorldAiAccountIds,
        TimeSpan? gapSincePreviousMessage)
    {
        ReplyTarget = replyTarget;
        Messages = messages;
        Memories = memories;
        SelfMemories = selfMemories;
        WorldConversationContext = worldConversationContext;
        GroupWorldConversationContext = groupWorldConversationContext;
        CrossWorldAiAccountIds = crossWorldAiAccountIds;
        GapSincePreviousMessage = gapSincePreviousMessage;
    }
}

/// <summary>
/// 使用发送者类型和账号 Id 构建身份隔离的模型上下文。
/// </summary>
public sealed class AiConversationContextBuilder
{
    private const int MaximumMemoryCount = 4;
    private const int MaximumSelfMemoryCount = 12;

    public AiConversationContext Build(
        AiMessageGenerationRequest request,
        int recentMessageLimit)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (recentMessageLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentMessageLimit));
        }

        AiDialogueMessage? replyTargetMessage = request.ReplyTarget?.Message;
        Guid? relationshipTargetId = request.RelationshipTarget?.Id
            ?? replyTargetMessage?.SenderAiAccountId;
        Dictionary<Guid, Guid> participantWorldIds = request.OtherParticipants
            .Append(request.Speaker)
            .GroupBy(account => account.Id)
            .ToDictionary(
                group => group.Key,
                group => group.First().CharacterWorldId);
        AiConversationContextMessage? replyTarget = replyTargetMessage is null
            ? null
            : CreateContextMessage(
                replyTargetMessage,
                request.Speaker.Id,
                relationshipTargetId,
                participantWorldIds);

        List<AiConversationContextMessage> messages = request.RecentMessages
            .Where(message => !IsSameMessage(message, replyTargetMessage))
            .TakeLast(recentMessageLimit)
            .Select(message => CreateContextMessage(
                message,
                request.Speaker.Id,
                relationshipTargetId,
                participantWorldIds))
            .ToList();
        TimeSpan? gapSincePreviousMessage = CalculateGapSincePreviousMessage(
            replyTargetMessage,
            request.RecentMessages);

        HashSet<Guid> participantIds = request.OtherParticipants
            .Select(participant => participant.Id)
            .ToHashSet();
        List<AiConversationMemory> memories = request.RelevantMemories
            .Where(memory =>
                memory.OwnerAiAccountId == request.Speaker.Id
                && relationshipTargetId is not null
                && memory.SubjectAiAccountId == relationshipTargetId
                && participantIds.Contains(memory.SubjectAiAccountId)
                && !string.IsNullOrWhiteSpace(memory.Summary))
            .Take(MaximumMemoryCount)
            .ToList();
        List<AiConversationSelfMemory> selfMemories = request
            .RelevantSelfMemories
            .Where(memory =>
                memory.AiAccountId == request.Speaker.Id
                && !string.IsNullOrWhiteSpace(memory.Summary))
            .OrderByDescending(memory => memory.IsProtectedFact)
            .ThenByDescending(memory => memory.IsUserLocked)
            .ThenByDescending(memory =>
                memory.TrustLevel == AiSelfMemoryTrustLevel.UserCanon)
            .ThenByDescending(memory => memory.Salience)
            .ThenByDescending(memory => memory.UpdatedAt)
            .ThenBy(memory => memory.Id)
            .Take(MaximumSelfMemoryCount)
            .ToList();
        AiWorldConversationContext? worldConversationContext =
            ValidateWorldConversationContext(request);
        AiGroupWorldConversationContext? groupWorldConversationContext =
            ValidateGroupWorldConversationContext(request);
        List<Guid> crossWorldAiAccountIds = new();
        if (worldConversationContext?.RelationshipAwareness ==
                AiWorldAwarenessState.CrossWorldConfirmed
            && worldConversationContext.SubjectAiAccountId
                is Guid confirmedSubjectId)
        {
            crossWorldAiAccountIds.Add(confirmedSubjectId);
        }
        if (groupWorldConversationContext is not null)
        {
            crossWorldAiAccountIds.AddRange(groupWorldConversationContext
                .ParticipantContexts
                .Where(context =>
                    context.RelationshipAwareness ==
                        AiWorldAwarenessState.CrossWorldConfirmed
                    && context.SubjectAiAccountId is not null)
                .Select(context => context.SubjectAiAccountId!.Value));
        }
        crossWorldAiAccountIds = crossWorldAiAccountIds
            .Distinct()
            .ToList();

        return new AiConversationContext(
            replyTarget,
            messages.AsReadOnly(),
            memories.AsReadOnly(),
            selfMemories.AsReadOnly(),
            worldConversationContext,
            groupWorldConversationContext,
            crossWorldAiAccountIds.AsReadOnly(),
            gapSincePreviousMessage);
    }

    /// <summary>
    /// 防止调用方把其他账号的知识、错误世界作用域或过量知识直接放入提示词。
    /// </summary>
    private static AiWorldConversationContext?
        ValidateWorldConversationContext(
            AiMessageGenerationRequest request)
    {
        AiWorldConversationContext? context =
            request.WorldConversationContext;
        if (context is null)
        {
            return null;
        }

        AiAccount? subject = request.RelationshipTarget;
        if (subject is null)
        {
            return context with
            {
                RelationshipAwareness =
                    AiWorldAwarenessState.AssumedSharedWorld,
                SubjectAiAccountId = null,
                SubjectCharacterWorldId = null,
                VisibleSubjectWorldName = null,
                InquiryMode = AiWorldInquiryMode.None,
                RelevantKnowledge =
                    Array.Empty<AiConversationWorldKnowledge>()
            };
        }

        if (context.SubjectAiAccountId != subject.Id
            || context.SubjectCharacterWorldId !=
                subject.CharacterWorldId)
        {
            return null;
        }

        IReadOnlyList<AiConversationWorldKnowledge> knowledge =
            context.RelationshipAwareness ==
                AiWorldAwarenessState.AssumedSharedWorld
                ? Array.Empty<AiConversationWorldKnowledge>()
                : context.RelevantKnowledge
                    .Where(item =>
                        item.OwnerAiAccountId == request.Speaker.Id
                        && item.SubjectAiAccountId == subject.Id
                        && item.SubjectCharacterWorldId ==
                            subject.CharacterWorldId
                        && !string.IsNullOrWhiteSpace(item.Summary))
                    .Take(4)
                    .ToList()
                    .AsReadOnly();
        string? visibleWorldName =
            context.RelationshipAwareness ==
                AiWorldAwarenessState.CrossWorldConfirmed
                ? context.VisibleSubjectWorldName
                : null;

        return context with
        {
            VisibleSubjectWorldName = visibleWorldName,
            RelevantKnowledge = knowledge
        };
    }

    /// <summary>
    /// 逐成员校验群聊世界上下文，过滤错误所有者、群外对象和过量知识。
    /// </summary>
    private static AiGroupWorldConversationContext?
        ValidateGroupWorldConversationContext(
            AiMessageGenerationRequest request)
    {
        AiGroupWorldConversationContext? context =
            request.GroupWorldConversationContext;
        if (context is null)
        {
            return null;
        }

        Dictionary<Guid, AiAccount> participants = request
            .OtherParticipants
            .Where(account => account.Id != request.Speaker.Id)
            .GroupBy(account => account.Id)
            .ToDictionary(group => group.Key, group => group.First());
        List<AiWorldConversationContext> validated = new();

        foreach (AiWorldConversationContext participantContext in context
                     .ParticipantContexts)
        {
            if (participantContext.SubjectAiAccountId is not Guid subjectId
                || !participants.TryGetValue(
                    subjectId,
                    out AiAccount? subject)
                || participantContext.SubjectCharacterWorldId !=
                    subject.CharacterWorldId
                || validated.Any(item =>
                    item.SubjectAiAccountId == subjectId))
            {
                continue;
            }

            IReadOnlyList<AiConversationWorldKnowledge> knowledge =
                participantContext.RelationshipAwareness ==
                    AiWorldAwarenessState.AssumedSharedWorld
                    ? Array.Empty<AiConversationWorldKnowledge>()
                    : participantContext.RelevantKnowledge
                        .Where(item =>
                            item.OwnerAiAccountId == request.Speaker.Id
                            && item.SubjectAiAccountId == subjectId
                            && item.SubjectCharacterWorldId ==
                                subject.CharacterWorldId
                            && !string.IsNullOrWhiteSpace(item.Summary))
                        .Take(4)
                        .ToList()
                        .AsReadOnly();
            string? visibleWorldName =
                participantContext.RelationshipAwareness ==
                    AiWorldAwarenessState.CrossWorldConfirmed
                    ? participantContext.VisibleSubjectWorldName
                    : null;

            validated.Add(participantContext with
            {
                ParallelWorldAwareness = context.ParallelWorldAwareness,
                IsNewlyInformedByCurrentMessage =
                    context.IsNewlyInformedByCurrentMessage,
                VisibleSubjectWorldName = visibleWorldName,
                RelevantKnowledge = knowledge
            });
        }

        return context with
        {
            ParticipantContexts = validated.AsReadOnly()
        };
    }

    private static AiConversationContextMessage CreateContextMessage(
        AiDialogueMessage message,
        Guid currentSpeakerId,
        Guid? relationshipTargetId,
        IReadOnlyDictionary<Guid, Guid> participantWorldIds)
    {
        AiConversationMessageOwnership ownership = ResolveOwnership(
            message,
            currentSpeakerId,
            relationshipTargetId);
        Guid? characterWorldId = message.SenderAiAccountId is Guid accountId
            && participantWorldIds.TryGetValue(accountId, out Guid worldId)
                ? worldId
                : null;

        return new AiConversationContextMessage(
            message,
            ownership,
            characterWorldId);
    }

    private static TimeSpan? CalculateGapSincePreviousMessage(
        AiDialogueMessage? replyTarget,
        IReadOnlyList<AiDialogueMessage> recentMessages)
    {
        if (replyTarget is null || replyTarget.SentAt == default)
        {
            return null;
        }

        DateTime? previousSentAt = recentMessages
            .Where(message =>
                !IsSameMessage(message, replyTarget)
                && message.SentAt != default
                && message.SentAt < replyTarget.SentAt)
            .Select(message => (DateTime?)message.SentAt)
            .Max();

        if (previousSentAt is null)
        {
            return null;
        }

        TimeSpan gap = replyTarget.SentAt - previousSentAt.Value;
        return gap > TimeSpan.Zero ? gap : null;
    }

    private static bool IsSameMessage(
        AiDialogueMessage message,
        AiDialogueMessage? other)
    {
        if (other is null)
        {
            return false;
        }

        if (message.MessageId != Guid.Empty && other.MessageId != Guid.Empty)
        {
            return message.MessageId == other.MessageId;
        }

        return message == other;
    }

    private static AiConversationMessageOwnership ResolveOwnership(
        AiDialogueMessage message,
        Guid currentSpeakerId,
        Guid? relationshipTargetId)
    {
        if (message.SenderType == MessageSenderType.User)
        {
            return AiConversationMessageOwnership.LocalUser;
        }

        if (message.SenderAiAccountId == currentSpeakerId)
        {
            return AiConversationMessageOwnership.CurrentSpeaker;
        }

        return relationshipTargetId is not null
               && message.SenderAiAccountId == relationshipTargetId
            ? AiConversationMessageOwnership.ReplyTargetAiAccount
            : AiConversationMessageOwnership.OtherAiAccount;
    }
}

/// <summary>
/// 将消息时间差转换为模型容易理解、但不过度精确的对话时间语义。
/// </summary>
internal static class AiConversationTimeGapFormatter
{
    public static string Format(TimeSpan? gap)
    {
        if (gap is null)
        {
            return "未知（不要自行假定刚刚连续聊过）";
        }

        if (gap.Value < TimeSpan.FromMinutes(5))
        {
            return "不足 5 分钟，属于连续交流";
        }

        if (gap.Value < TimeSpan.FromHours(1))
        {
            return $"约 {Math.Max(5, (int)Math.Round(gap.Value.TotalMinutes / 5) * 5)} 分钟";
        }

        if (gap.Value < TimeSpan.FromHours(24))
        {
            return $"约 {Math.Max(1, (int)Math.Round(gap.Value.TotalHours))} 小时";
        }

        return $"约 {Math.Max(1, (int)Math.Round(gap.Value.TotalDays))} 天，属于跨时间续聊";
    }
}

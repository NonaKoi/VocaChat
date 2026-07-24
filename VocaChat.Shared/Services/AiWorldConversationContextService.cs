using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 按当前发言者的方向性认知和当前话题，准备可供私聊导演与生成器使用的世界上下文。
/// </summary>
public sealed class AiWorldConversationContextService
{
    private const int MaximumCandidateKnowledgeCount = 100;
    private const int MaximumRecalledKnowledgeCount = 4;
    private const int MaximumGroupParticipantContextCount = 8;

    private readonly VocaChatDbContextFactory _dbContextFactory;
    private readonly AiWorldAwarenessService _awarenessService;
    private readonly AiWorldKnowledgeService _knowledgeService;
    private readonly AiWorldKnowledgeCandidateExtractor _candidateExtractor;

    public AiWorldConversationContextService(
        VocaChatDbContextFactory dbContextFactory,
        AiWorldAwarenessService awarenessService,
        AiWorldKnowledgeService knowledgeService,
        AiWorldKnowledgeCandidateExtractor candidateExtractor)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _awarenessService = awarenessService
            ?? throw new ArgumentNullException(nameof(awarenessService));
        _knowledgeService = knowledgeService
            ?? throw new ArgumentNullException(nameof(knowledgeService));
        _candidateExtractor = candidateExtractor
            ?? throw new ArgumentNullException(nameof(candidateExtractor));
    }

    /// <summary>
    /// 返回带有当前发言者世界认知的生成请求。读取失败时保持保守的未知状态，
    /// 不阻断已经建立的聊天流程。
    /// </summary>
    public AiMessageGenerationRequest PrepareGenerationRequest(
        AiMessageGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        AiParallelWorldAwarenessState parallelState =
            ReadParallelWorldAwareness(
                request.Speaker.Id,
                out AiParallelWorldAwareness? parallelAwareness);
        Guid currentMessageId =
            request.ReplyTarget?.Message?.MessageId ?? Guid.Empty;
        bool newlyInformed = parallelState
                != AiParallelWorldAwarenessState.Unaware
            && currentMessageId != Guid.Empty
            && (parallelAwareness?.LastSourcePrivateMessageId
                    == currentMessageId
                || parallelAwareness?.LastSourceGroupMessageId
                    == currentMessageId);

        AiAccount? subject = request.RelationshipTarget;
        if (subject is null || subject.Id == request.Speaker.Id)
        {
            return request with
            {
                WorldConversationContext = new AiWorldConversationContext(
                    parallelState,
                    AiWorldAwarenessState.AssumedSharedWorld,
                    SubjectAiAccountId: null,
                    SubjectCharacterWorldId: null,
                    VisibleSubjectWorldName: null,
                    newlyInformed,
                    AiWorldInquiryMode.None,
                    Array.Empty<AiConversationWorldKnowledge>())
            };
        }

        AiWorldConversationContext subjectContext = BuildSubjectContext(
            request,
            subject,
            parallelState,
            newlyInformed);

        return request with
        {
            WorldConversationContext = subjectContext
        };
    }

    /// <summary>
    /// 为群聊中的当前发言者分别准备相关成员的方向性认知。
    /// 未进入结果的成员不会通过真实角色世界关系被自动补全。
    /// </summary>
    public AiMessageGenerationRequest PrepareGroupGenerationRequest(
        AiMessageGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<AiAccount> subjects = ResolveGroupSubjects(request);
        AiParallelWorldAwarenessState parallelState =
            AiParallelWorldAwarenessState.Unaware;
        bool newlyInformed = false;
        IReadOnlyList<AiWorldConversationContext> participantContexts;

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiParallelWorldAwareness? parallelAwareness = dbContext
                .AiParallelWorldAwareness
                .AsNoTracking()
                .SingleOrDefault(item =>
                    item.AiAccountId == request.Speaker.Id);
            parallelState = parallelAwareness?.State
                ?? AiParallelWorldAwarenessState.Unaware;
            Guid currentMessageId =
                request.ReplyTarget?.Message?.MessageId ?? Guid.Empty;
            newlyInformed = parallelState
                    != AiParallelWorldAwarenessState.Unaware
                && currentMessageId != Guid.Empty
                && (parallelAwareness?.LastSourcePrivateMessageId
                        == currentMessageId
                    || parallelAwareness?.LastSourceGroupMessageId
                        == currentMessageId);
            List<Guid> subjectIds = subjects
                .Select(subject => subject.Id)
                .ToList();
            Dictionary<Guid, AiWorldAwarenessState> awarenessStates =
                dbContext.AiWorldAwareness
                    .AsNoTracking()
                    .Where(item =>
                        item.ObserverAiAccountId == request.Speaker.Id
                        && subjectIds.Contains(item.SubjectAiAccountId))
                    .ToDictionary(
                        item => item.SubjectAiAccountId,
                        item => item.State);
            List<AiWorldKnowledge> knowledge = dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Where(item =>
                    item.OwnerAiAccountId == request.Speaker.Id
                    && item.SubjectAiAccountId != null
                    && subjectIds.Contains(
                        item.SubjectAiAccountId.Value)
                    && item.Status == AiWorldKnowledgeStatus.Active)
                .OrderByDescending(item => item.UpdatedAt)
                .Take(
                    MaximumCandidateKnowledgeCount
                    * MaximumGroupParticipantContextCount)
                .ToList();
            List<Guid> worldIds = subjects
                .Select(subject => subject.CharacterWorldId)
                .Distinct()
                .ToList();
            Dictionary<Guid, string> worldNames = dbContext.CharacterWorlds
                .AsNoTracking()
                .Where(world => worldIds.Contains(world.Id))
                .ToDictionary(world => world.Id, world => world.Name);
            string queryText = BuildQueryText(request);

            participantContexts = subjects
                .Select(subject => BuildGroupSubjectContext(
                    request,
                    subject,
                    parallelState,
                    newlyInformed,
                    awarenessStates.GetValueOrDefault(
                        subject.Id,
                        AiWorldAwarenessState.AssumedSharedWorld),
                    knowledge.Where(item =>
                        item.SubjectAiAccountId == subject.Id),
                    worldNames.GetValueOrDefault(
                        subject.CharacterWorldId),
                    queryText))
                .ToList()
                .AsReadOnly();
        }
        catch
        {
            participantContexts =
                Array.Empty<AiWorldConversationContext>();
        }

        AiWorldConversationContext? primaryContext =
            request.RelationshipTarget is null
                ? null
                : participantContexts.SingleOrDefault(context =>
                    context.SubjectAiAccountId ==
                        request.RelationshipTarget.Id);

        return request with
        {
            WorldConversationContext = primaryContext
                ?? new AiWorldConversationContext(
                    parallelState,
                    AiWorldAwarenessState.AssumedSharedWorld,
                    SubjectAiAccountId: null,
                    SubjectCharacterWorldId: null,
                    VisibleSubjectWorldName: null,
                    newlyInformed,
                    AiWorldInquiryMode.None,
                    Array.Empty<AiConversationWorldKnowledge>()),
            GroupWorldConversationContext =
                new AiGroupWorldConversationContext(
                    parallelState,
                    newlyInformed,
                    participantContexts)
        };
    }

    private AiWorldConversationContext BuildGroupSubjectContext(
        AiMessageGenerationRequest request,
        AiAccount subject,
        AiParallelWorldAwarenessState parallelState,
        bool newlyInformed,
        AiWorldAwarenessState relationshipState,
        IEnumerable<AiWorldKnowledge> candidateKnowledge,
        string? worldName,
        string queryText)
    {
        int maximumCount = relationshipState switch
        {
            AiWorldAwarenessState.AnomalyObserved => 2,
            AiWorldAwarenessState.DifferentBackgroundRecognized => 3,
            AiWorldAwarenessState.CrossWorldConfirmed =>
                MaximumRecalledKnowledgeCount,
            _ => 0
        };
        IReadOnlyList<AiConversationWorldKnowledge> relevantKnowledge =
            relationshipState == AiWorldAwarenessState.AssumedSharedWorld
                ? Array.Empty<AiConversationWorldKnowledge>()
                : candidateKnowledge
                    .Select(knowledge => new
                    {
                        Knowledge = knowledge,
                        Relevance = CalculateRelevance(
                            knowledge,
                            queryText)
                    })
                    .Where(candidate => candidate.Relevance > 0)
                    .OrderByDescending(candidate => candidate.Relevance)
                    .ThenByDescending(candidate =>
                        candidate.Knowledge.Salience)
                    .ThenByDescending(candidate =>
                        candidate.Knowledge.UpdatedAt)
                    .ThenBy(candidate => candidate.Knowledge.Id)
                    .Take(maximumCount)
                    .Select(candidate =>
                        ToConversationWorldKnowledge(
                            candidate.Knowledge))
                    .ToList()
                    .AsReadOnly();
        string? visibleWorldName =
            relationshipState ==
                AiWorldAwarenessState.CrossWorldConfirmed
            && !string.IsNullOrWhiteSpace(worldName)
            && (queryText.Contains(
                    worldName,
                    StringComparison.OrdinalIgnoreCase)
                || candidateKnowledge.Any(item => item.Summary.Contains(
                    worldName,
                    StringComparison.OrdinalIgnoreCase)))
                ? worldName
                : null;

        return new AiWorldConversationContext(
            parallelState,
            relationshipState,
            subject.Id,
            subject.CharacterWorldId,
            visibleWorldName,
            newlyInformed,
            ResolveInquiryMode(request, subject, relationshipState),
            relevantKnowledge);
    }

    private static AiConversationWorldKnowledge
        ToConversationWorldKnowledge(AiWorldKnowledge knowledge)
    {
        return new AiConversationWorldKnowledge(
            knowledge.Id,
            knowledge.OwnerAiAccountId,
            knowledge.SubjectCharacterWorldId,
            knowledge.SubjectAiAccountId,
            knowledge.Summary,
            knowledge.TrustLevel,
            knowledge.Salience,
            knowledge.UpdatedAt);
    }

    private AiWorldConversationContext BuildSubjectContext(
        AiMessageGenerationRequest request,
        AiAccount subject,
        AiParallelWorldAwarenessState parallelState,
        bool newlyInformed)
    {
        AiWorldAwarenessState relationshipState =
            ReadRelationshipAwareness(request.Speaker.Id, subject.Id);
        IReadOnlyList<AiConversationWorldKnowledge> recalled =
            RecallRelevantKnowledge(
                request,
                subject,
                relationshipState);
        string? visibleWorldName = ReadVisibleCharacterWorldName(
            request,
            subject,
            relationshipState);
        AiWorldInquiryMode inquiryMode = ResolveInquiryMode(
            request,
            subject,
            relationshipState);

        return new AiWorldConversationContext(
            parallelState,
            relationshipState,
            subject.Id,
            subject.CharacterWorldId,
            visibleWorldName,
            newlyInformed,
            inquiryMode,
            recalled);
    }

    private static IReadOnlyList<AiAccount> ResolveGroupSubjects(
        AiMessageGenerationRequest request)
    {
        Dictionary<Guid, AiAccount> participants = request
            .OtherParticipants
            .Where(account => account.Id != request.Speaker.Id)
            .GroupBy(account => account.Id)
            .ToDictionary(group => group.Key, group => group.First());
        List<Guid> priorities = new();

        AddPriority(priorities, request.RelationshipTarget?.Id, participants);
        AddPriority(
            priorities,
            request.ReplyTarget?.Message?.SenderAiAccountId,
            participants);

        string queryText = BuildQueryText(request);
        foreach (AiAccount participant in participants.Values)
        {
            if (queryText.Contains(
                    $"@{participant.Nickname}",
                    StringComparison.OrdinalIgnoreCase))
            {
                AddPriority(priorities, participant.Id, participants);
            }
        }

        foreach (Guid speakerId in request.RecentMessages
                     .Where(message =>
                         message.SenderAiAccountId is not null)
                     .Reverse()
                     .Select(message => message.SenderAiAccountId!.Value))
        {
            AddPriority(priorities, speakerId, participants);
        }

        foreach (AiAccount participant in participants.Values)
        {
            AddPriority(priorities, participant.Id, participants);
        }

        return priorities
            .Take(MaximumGroupParticipantContextCount)
            .Select(id => participants[id])
            .ToList()
            .AsReadOnly();
    }

    private static void AddPriority(
        ICollection<Guid> priorities,
        Guid? aiAccountId,
        IReadOnlyDictionary<Guid, AiAccount> participants)
    {
        if (aiAccountId is Guid id
            && participants.ContainsKey(id)
            && !priorities.Contains(id))
        {
            priorities.Add(id);
        }
    }

    private AiParallelWorldAwarenessState ReadParallelWorldAwareness(
        Guid aiAccountId,
        out AiParallelWorldAwareness? awareness)
    {
        AiWorldAwarenessOperationStatus status =
            _awarenessService.TryGetParallelWorldAwareness(
                aiAccountId,
                out AiParallelWorldAwarenessState state,
                out awareness,
                out _);
        return status == AiWorldAwarenessOperationStatus.Success
            ? state
            : AiParallelWorldAwarenessState.Unaware;
    }

    private AiWorldAwarenessState ReadRelationshipAwareness(
        Guid observerAiAccountId,
        Guid subjectAiAccountId)
    {
        AiWorldAwarenessOperationStatus status =
            _awarenessService.TryGetWorldAwareness(
                observerAiAccountId,
                subjectAiAccountId,
                out AiWorldAwarenessState state,
                out _,
                out _);
        return status == AiWorldAwarenessOperationStatus.Success
            ? state
            : AiWorldAwarenessState.AssumedSharedWorld;
    }

    private IReadOnlyList<AiConversationWorldKnowledge>
        RecallRelevantKnowledge(
            AiMessageGenerationRequest request,
            AiAccount subject,
            AiWorldAwarenessState relationshipState)
    {
        if (relationshipState == AiWorldAwarenessState.AssumedSharedWorld)
        {
            return Array.Empty<AiConversationWorldKnowledge>();
        }

        AiWorldKnowledgeOperationStatus status =
            _knowledgeService.TryGetActiveKnowledge(
                request.Speaker.Id,
                subject.CharacterWorldId,
                subject.Id,
                MaximumCandidateKnowledgeCount,
                out IReadOnlyList<AiWorldKnowledge> candidates,
                out _);
        if (status != AiWorldKnowledgeOperationStatus.Success
            || candidates.Count == 0)
        {
            return Array.Empty<AiConversationWorldKnowledge>();
        }

        string queryText = BuildQueryText(request);
        int maximumCount = relationshipState switch
        {
            AiWorldAwarenessState.AnomalyObserved => 2,
            AiWorldAwarenessState.DifferentBackgroundRecognized => 3,
            AiWorldAwarenessState.CrossWorldConfirmed =>
                MaximumRecalledKnowledgeCount,
            _ => 0
        };

        return candidates
            .Select(knowledge => new
            {
                Knowledge = knowledge,
                Relevance = CalculateRelevance(knowledge, queryText)
            })
            .Where(candidate => candidate.Relevance > 0)
            .OrderByDescending(candidate => candidate.Relevance)
            .ThenByDescending(candidate => candidate.Knowledge.Salience)
            .ThenByDescending(candidate => candidate.Knowledge.UpdatedAt)
            .ThenBy(candidate => candidate.Knowledge.Id)
            .Take(maximumCount)
            .Select(candidate => new AiConversationWorldKnowledge(
                candidate.Knowledge.Id,
                candidate.Knowledge.OwnerAiAccountId,
                candidate.Knowledge.SubjectCharacterWorldId,
                candidate.Knowledge.SubjectAiAccountId,
                candidate.Knowledge.Summary,
                candidate.Knowledge.TrustLevel,
                candidate.Knowledge.Salience,
                candidate.Knowledge.UpdatedAt))
            .ToList()
            .AsReadOnly();
    }

    private AiWorldInquiryMode ResolveInquiryMode(
        AiMessageGenerationRequest request,
        AiAccount subject,
        AiWorldAwarenessState relationshipState)
    {
        if (request.QuestionPolicy?.ForceDeclarativeReply == true
            || request.Scenario ==
                AiMessageGenerationScenario.AutonomousPrivateChatClosing)
        {
            return AiWorldInquiryMode.None;
        }

        string focus = string.Join(
            Environment.NewLine,
            request.FocusContent,
            request.ReplyTarget?.Message?.Content,
            request.Topic);
        AiWorldKnowledgeSignal signal =
            _candidateExtractor.Extract(subject, focus).Signal;

        return relationshipState switch
        {
            AiWorldAwarenessState.AssumedSharedWorld
                when signal == AiWorldKnowledgeSignal.UnfamiliarConcept =>
                    AiWorldInquiryMode.ClarifyUnfamiliarConcept,
            AiWorldAwarenessState.AnomalyObserved
                when signal is AiWorldKnowledgeSignal.UnfamiliarConcept
                    or AiWorldKnowledgeSignal.BackgroundDifference =>
                    AiWorldInquiryMode.ExploreBackgroundDifference,
            AiWorldAwarenessState.DifferentBackgroundRecognized
                when signal != AiWorldKnowledgeSignal.None =>
                    AiWorldInquiryMode.ExploreBackgroundDifference,
            AiWorldAwarenessState.CrossWorldConfirmed
                when signal != AiWorldKnowledgeSignal.None =>
                    AiWorldInquiryMode.DiscussConfirmedWorld,
            _ => AiWorldInquiryMode.None
        };
    }

    private string? ReadCharacterWorldName(Guid characterWorldId)
    {
        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            return dbContext.CharacterWorlds
                .Where(world => world.Id == characterWorldId)
                .Select(world => world.Name)
                .SingleOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private string? ReadVisibleCharacterWorldName(
        AiMessageGenerationRequest request,
        AiAccount subject,
        AiWorldAwarenessState relationshipState)
    {
        if (relationshipState !=
            AiWorldAwarenessState.CrossWorldConfirmed)
        {
            return null;
        }

        string? worldName = ReadCharacterWorldName(
            subject.CharacterWorldId);
        if (string.IsNullOrWhiteSpace(worldName))
        {
            return null;
        }

        if (BuildQueryText(request).Contains(
                worldName,
                StringComparison.OrdinalIgnoreCase))
        {
            return worldName;
        }

        AiWorldKnowledgeOperationStatus status =
            _knowledgeService.TryGetActiveKnowledge(
                request.Speaker.Id,
                subject.CharacterWorldId,
                subject.Id,
                MaximumCandidateKnowledgeCount,
                out IReadOnlyList<AiWorldKnowledge> knowledge,
                out _);
        return status == AiWorldKnowledgeOperationStatus.Success
            && knowledge.Any(item => item.Summary.Contains(
                worldName,
                StringComparison.OrdinalIgnoreCase))
            ? worldName
            : null;
    }

    private static string BuildQueryText(
        AiMessageGenerationRequest request)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                request.FocusContent,
                request.Topic,
                request.ReplyTarget?.Message?.Content ?? string.Empty
            }
            .Concat(request.RecentMessages
                .TakeLast(6)
                .Select(message => message.Content))
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int CalculateRelevance(
        AiWorldKnowledge knowledge,
        string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return 0;
        }

        int relevance = 0;
        bool hasTopicMatch = false;
        if (AiFactGroundingMatcher.HasGroundingOverlap(
                queryText,
                knowledge.Summary))
        {
            relevance += 100;
            hasTopicMatch = true;
        }

        foreach (string fragment in ExtractSearchFragments(
                     knowledge.Summary))
        {
            if (queryText.Contains(
                    fragment,
                    StringComparison.OrdinalIgnoreCase))
            {
                relevance += Math.Min(40, fragment.Length * 4);
                hasTopicMatch = true;
            }
        }

        if (!hasTopicMatch)
        {
            return 0;
        }

        relevance += knowledge.TrustLevel switch
        {
            AiWorldKnowledgeTrustLevel.UserConfirmed => 12,
            AiWorldKnowledgeTrustLevel.Corroborated => 10,
            AiWorldKnowledgeTrustLevel.DirectStatement => 8,
            AiWorldKnowledgeTrustLevel.Unverified => 2,
            _ => 0
        };
        relevance += knowledge.Salience / 20;
        return relevance;
    }

    private static IEnumerable<string> ExtractSearchFragments(
        string value)
    {
        string normalized = value
            .Replace("提到：", " ", StringComparison.Ordinal)
            .Replace("本地用户明确说明", " ", StringComparison.Ordinal);
        char[] separators =
        {
            ' ', '\r', '\n', '\t', '，', '。', '！', '？', '：', '；',
            ',', '.', '!', '?', ':', ';', '“', '”', '"', '（', '）',
            '(', ')'
        };

        return normalized
            .Split(
                separators,
                StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
            .Where(fragment => fragment.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

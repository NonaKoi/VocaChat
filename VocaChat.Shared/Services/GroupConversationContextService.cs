using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 保存一位候选发言者与一个潜在回应对象之间的双向关系摘要，
/// 以及只属于候选发言者自己的方向记忆。
/// </summary>
public sealed record GroupConversationRelationshipContext(
    Guid TargetAiAccountId,
    string TargetDisplayName,
    double SpeakerToTargetScore,
    double TargetToSpeakerScore,
    IReadOnlyList<AiConversationMemory> RelevantMemories);

/// <summary>
/// 保存群级导演可以使用的一位候选发言者的有限身份上下文。
/// </summary>
public sealed record GroupConversationCandidateContext(
    Guid AiAccountId,
    IReadOnlyList<AiConversationSelfMemory> RelevantSelfMemories,
    IReadOnlyList<GroupConversationRelationshipContext> Relationships);

/// <summary>
/// 为群级导演和实际发言者组装同一套有方向关系、关系记忆和个人记忆边界。
/// </summary>
public sealed class GroupConversationContextService
{
    private const int MaximumCandidateSelfMemoryCount = 2;
    private const int MaximumCandidateTargetCount = 2;
    private const int MaximumCandidateRelationshipMemoryCount = 2;
    private const int MaximumSpeakerRelationshipMemoryCount = 4;

    private readonly VocaChatDbContextFactory _dbContextFactory;
    private readonly AiIdentityContinuityService _identityContinuityService;

    public GroupConversationContextService(
        VocaChatDbContextFactory dbContextFactory,
        AiIdentityContinuityService identityContinuityService)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _identityContinuityService = identityContinuityService
            ?? throw new ArgumentNullException(
                nameof(identityContinuityService));
    }

    /// <summary>
    /// 为群级导演准备有限候选上下文；只考虑点名成员、最近发言者和规则建议的主要成员。
    /// </summary>
    public IReadOnlyList<GroupConversationCandidateContext>
        BuildCandidateContexts(
            GroupConversationPlanningRequest request,
            IReadOnlyList<AiAccount> candidatePool,
            GroupConversationTurnPlan fallbackPlan)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidatePool);
        ArgumentNullException.ThrowIfNull(fallbackPlan);

        IReadOnlyList<Guid> targetPriority = ResolveTargetPriority(
            request,
            fallbackPlan);
        IReadOnlyList<AiDialogueMessage> recentMessages = request
            .RecentMessages
            .Select(ToDialogueMessage)
            .ToList()
            .AsReadOnly();
        AiDialogueMessage? anchorMessage = request.AnchorMessage is null
            ? null
            : ToDialogueMessage(request.AnchorMessage);
        string focusContent = ResolveFocusContent(request);
        List<GroupConversationCandidateContext> contexts = new();

        foreach (AiAccount candidate in candidatePool)
        {
            AiMessageGenerationRequest candidateRequest = new()
            {
                Scenario = request.Scenario ==
                        GroupConversationPlanningScenario.UserMessage
                    ? AiMessageGenerationScenario.GroupPrimaryReply
                    : AiMessageGenerationScenario.AutonomousGroupChat,
                Speaker = candidate,
                OtherParticipants = request.GroupChat.Members
                    .Where(member => member.Id != candidate.Id)
                    .ToList()
                    .AsReadOnly(),
                Topic = focusContent,
                FocusContent = focusContent,
                ReplyTarget = anchorMessage is null
                    ? AiDialogueReplyTarget.OpenTopic()
                    : AiDialogueReplyTarget.ReplyTo(anchorMessage),
                ConversationAnchor = anchorMessage,
                RecentMessages = recentMessages
            };
            AiMessageGenerationRequest preparedCandidate =
                _identityContinuityService.PrepareGenerationRequest(
                    candidateRequest);
            IReadOnlyList<GroupConversationRelationshipContext>
                relationships = targetPriority
                    .Where(targetId => targetId != candidate.Id)
                    .Select(targetId => request.GroupChat.Members
                        .SingleOrDefault(member => member.Id == targetId))
                    .Where(target => target is not null)
                    .Take(MaximumCandidateTargetCount)
                    .Select(target => LoadRelationshipContext(
                        candidate,
                        target!,
                        focusContent,
                        MaximumCandidateRelationshipMemoryCount))
                    .ToList()
                    .AsReadOnly();

            contexts.Add(new GroupConversationCandidateContext(
                candidate.Id,
                preparedCandidate.RelevantSelfMemories
                    .Take(MaximumCandidateSelfMemoryCount)
                    .ToList()
                    .AsReadOnly(),
                relationships));
        }

        return contexts.AsReadOnly();
    }

    /// <summary>
    /// 按实际回复目标为一位发言者加载双向关系、自己的方向记忆和自己的个人记忆。
    /// </summary>
    public AiMessageGenerationRequest PrepareGenerationRequest(
        AiMessageGenerationRequest request,
        AiAccount? relationshipTarget)
    {
        ArgumentNullException.ThrowIfNull(request);

        AiMessageGenerationRequest enrichedRequest = request with
        {
            RelationshipTarget = relationshipTarget,
            SpeakerToOtherRelationshipScore = null,
            OtherToSpeakerRelationshipScore = null,
            RelevantMemories = Array.Empty<AiConversationMemory>()
        };

        if (relationshipTarget is not null
            && relationshipTarget.Id != request.Speaker.Id)
        {
            try
            {
                GroupConversationRelationshipContext relationshipContext =
                    LoadRelationshipContext(
                        request.Speaker,
                        relationshipTarget,
                        BuildContextText(request),
                        MaximumSpeakerRelationshipMemoryCount);
                enrichedRequest = enrichedRequest with
                {
                    SpeakerToOtherRelationshipScore =
                        relationshipContext.SpeakerToTargetScore,
                    OtherToSpeakerRelationshipScore =
                        relationshipContext.TargetToSpeakerScore,
                    RelevantMemories = relationshipContext.RelevantMemories
                };
            }
            catch (SqliteException)
            {
                // 关系上下文是生成增强信息；读取失败时保留明确对象，
                // 但不使用可能不完整的关系或记忆阻断当前聊天。
            }
        }

        return _identityContinuityService.PrepareGenerationRequest(
            enrichedRequest);
    }

    private GroupConversationRelationshipContext LoadRelationshipContext(
        AiAccount speaker,
        AiAccount target,
        string contextText,
        int maximumMemoryCount)
    {
        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();
        List<AiRelationship> relationships = dbContext.AiRelationships
            .AsNoTracking()
            .Where(relationship =>
                (relationship.FromAiAccountId == speaker.Id
                 && relationship.ToAiAccountId == target.Id)
                || (relationship.FromAiAccountId == target.Id
                    && relationship.ToAiAccountId == speaker.Id))
            .ToList();
        AiRelationship speakerToTarget = relationships.SingleOrDefault(
                relationship =>
                    relationship.FromAiAccountId == speaker.Id)
            ?? new AiRelationship(speaker.Id, target.Id);
        AiRelationship targetToSpeaker = relationships.SingleOrDefault(
                relationship =>
                    relationship.FromAiAccountId == target.Id)
            ?? new AiRelationship(target.Id, speaker.Id);
        IReadOnlyList<AiConversationMemory> memories = dbContext.AiMemories
            .AsNoTracking()
            .Where(memory =>
                memory.OwnerAiAccountId == speaker.Id
                && memory.SubjectAiAccountId == target.Id
                && memory.IsActive)
            .ToList()
            .OrderByDescending(memory =>
                AiFactGroundingMatcher.HasGroundingOverlap(
                    memory.Summary,
                    contextText))
            .ThenByDescending(memory => memory.Salience)
            .ThenByDescending(memory => memory.OccurredAt)
            .ThenBy(memory => memory.Id)
            .Take(maximumMemoryCount)
            .Select(memory => new AiConversationMemory(
                memory.OwnerAiAccountId,
                memory.SubjectAiAccountId,
                target.Nickname,
                memory.Type,
                memory.Summary,
                memory.OccurredAt))
            .ToList()
            .AsReadOnly();

        return new GroupConversationRelationshipContext(
            target.Id,
            target.Nickname,
            AiRelationshipScoring.Calculate(speakerToTarget),
            AiRelationshipScoring.Calculate(targetToSpeaker),
            memories);
    }

    private static IReadOnlyList<Guid> ResolveTargetPriority(
        GroupConversationPlanningRequest request,
        GroupConversationTurnPlan fallbackPlan)
    {
        string anchorContent = request.AnchorMessage?.Content ?? string.Empty;
        IEnumerable<Guid> mentionedIds = request.Scenario ==
                GroupConversationPlanningScenario.UserMessage
            ? request.GroupChat.Members
            .Select(member => new
            {
                member.Id,
                Index = anchorContent.IndexOf(
                    $"@{member.Nickname}",
                    StringComparison.OrdinalIgnoreCase)
            })
            .Where(item => item.Index >= 0)
            .OrderBy(item => item.Index)
            .Select(item => item.Id)
            : Array.Empty<Guid>();
        IEnumerable<Guid> recentSpeakerIds = request.RecentMessages
            .Where(message =>
                message.SenderType == MessageSenderType.AiAccount
                && message.SenderAiAccountId is not null)
            .Reverse()
            .Select(message => message.SenderAiAccountId!.Value)
            .Distinct();
        IEnumerable<Guid> fallbackSpeakerIds = fallbackPlan.Speakers
            .Select(speaker => speaker.SpeakerAiAccountId);
        IEnumerable<Guid> preferredSpeakerIds = request
            .PreferredSpeakerAiAccountIds;
        HashSet<Guid> memberIds = request.GroupChat.Members
            .Select(member => member.Id)
            .ToHashSet();

        return mentionedIds
            .Concat(recentSpeakerIds)
            .Concat(preferredSpeakerIds)
            .Concat(fallbackSpeakerIds)
            .Where(memberIds.Contains)
            .Distinct()
            .ToList()
            .AsReadOnly();
    }

    private static string ResolveFocusContent(
        GroupConversationPlanningRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Topic))
        {
            return request.Topic.Trim();
        }

        return request.AnchorMessage?.Content.Trim() ?? "当前群聊话题";
    }

    private static string BuildContextText(
        AiMessageGenerationRequest request)
    {
        return string.Join(
            ' ',
            new[]
            {
                request.Topic,
                request.FocusContent,
                request.ReplyTarget?.Message?.Content ?? string.Empty
            }
                .Concat(request.RecentMessages
                    .TakeLast(3)
                    .Select(message => message.Content))
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static AiDialogueMessage ToDialogueMessage(
        GroupMessage message)
    {
        return new AiDialogueMessage(
            message.SenderDisplayName,
            message.Content,
            message.SenderType,
            message.SenderAiAccountId,
            message.Id,
            message.SentAt);
    }
}

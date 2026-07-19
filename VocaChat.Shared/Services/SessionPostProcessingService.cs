using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 协调一次已完成 Session 的洞察分析、关系演化和方向记忆保存。
/// </summary>
public sealed class SessionPostProcessingService
{
    private const int MediumMemorySalience = 70;
    private const int HighMemorySalience = 90;

    private readonly AutonomousPrivateChatSessionService _sessionService;
    private readonly AiAccountService _aiAccountService;
    private readonly ISessionInsightAnalyzer _insightAnalyzer;
    private readonly RelationshipEvolutionService _relationshipEvolutionService;
    private readonly AiMemoryService _memoryService;

    public SessionPostProcessingService(
        AutonomousPrivateChatSessionService sessionService,
        AiAccountService aiAccountService,
        ISessionInsightAnalyzer insightAnalyzer,
        RelationshipEvolutionService relationshipEvolutionService,
        AiMemoryService memoryService)
    {
        _sessionService = sessionService
            ?? throw new ArgumentNullException(nameof(sessionService));
        _aiAccountService = aiAccountService
            ?? throw new ArgumentNullException(nameof(aiAccountService));
        _insightAnalyzer = insightAnalyzer
            ?? throw new ArgumentNullException(nameof(insightAnalyzer));
        _relationshipEvolutionService = relationshipEvolutionService
            ?? throw new ArgumentNullException(
                nameof(relationshipEvolutionService));
        _memoryService = memoryService
            ?? throw new ArgumentNullException(nameof(memoryService));
    }

    /// <summary>
    /// 在消息和 Session 已经保存后执行后处理；记忆失败不回滚关系或消息。
    /// </summary>
    public async Task<SessionPostProcessingResult> ProcessAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        AutonomousPrivateChatSession? session =
            _sessionService.FindById(sessionId);

        if (session is null)
        {
            return CreateResult(
                SessionPostProcessingStatus.SessionNotFound,
                message: "自主私信 Session 不存在，不能执行后处理。");
        }

        if (session.Status != AutonomousPrivateChatSessionStatus.Completed
            || session.CompletedRounds == 0)
        {
            return CreateResult(
                SessionPostProcessingStatus.SessionNotEligible,
                message: "只有正常完成且至少完成一轮的自主私信才能执行后处理。");
        }

        RelationshipEvolutionStatus applicationStatus =
            _relationshipEvolutionService.GetApplicationStatus(
                session.Id,
                out IReadOnlyList<AiRelationshipChange> existingChanges,
                out string applicationError);

        if (applicationStatus == RelationshipEvolutionStatus.AlreadyApplied)
        {
            return CreateResult(
                SessionPostProcessingStatus.AlreadyProcessed,
                existingChanges,
                message: "当前 Session 已经完成关系和记忆后处理。");
        }

        if (applicationStatus != RelationshipEvolutionStatus.NotApplied)
        {
            return CreateResult(
                SessionPostProcessingStatus.RelationshipPersistenceFailed,
                existingChanges,
                message: applicationError);
        }

        AiAccount? initiator = _aiAccountService.FindById(
            session.InitiatorAiAccountId);
        AiAccount? recipient = _aiAccountService.FindById(
            session.RecipientAiAccountId);

        if (initiator is null || recipient is null)
        {
            return CreateResult(
                SessionPostProcessingStatus.ParticipantNotFound,
                message: "无法读取 Session 的完整参与者资料。");
        }

        IReadOnlyList<PrivateMessage> messages =
            _sessionService.GetMessages(session.Id);

        if (messages.Count == 0)
        {
            return CreateResult(
                SessionPostProcessingStatus.SessionNotEligible,
                message: "Session 没有可以分析的已保存消息。");
        }

        SessionInsightAnalysisRequest request = new(
            session,
            initiator,
            recipient,
            messages);
        SessionInsightAnalysis analysis;

        try
        {
            analysis = await _insightAnalyzer.AnalyzeAsync(
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            analysis = SessionInsightAnalysis.Fallback(
                "Session 语义分析异常，采用基础关系变化且不提取长期记忆。");
        }

        RelationshipEvolutionProposal proposal =
            SessionInsightRelationshipMapper.Map(
                analysis,
                messages.Select(message => message.Id).ToHashSet());
        RelationshipEvolutionStatus evolutionStatus =
            _relationshipEvolutionService.TryApplyCompletedSession(
                session.Id,
                proposal,
                out IReadOnlyList<AiRelationshipChange> relationshipChanges,
                out string evolutionError);

        if (evolutionStatus == RelationshipEvolutionStatus.AlreadyApplied)
        {
            return CreateResult(
                SessionPostProcessingStatus.AlreadyProcessed,
                relationshipChanges,
                analysis: analysis,
                message: "当前 Session 已经由另一个操作完成后处理。");
        }

        if (evolutionStatus != RelationshipEvolutionStatus.Success)
        {
            return CreateResult(
                SessionPostProcessingStatus.RelationshipPersistenceFailed,
                relationshipChanges,
                analysis: analysis,
                message: evolutionError);
        }

        List<AiMemory> savedMemories = new();
        List<string> memoryErrors = new();
        SaveDirectionMemories(
            session,
            initiator.Id,
            recipient.Id,
            analysis.InitiatorPerspective,
            messages,
            savedMemories,
            memoryErrors);
        SaveDirectionMemories(
            session,
            recipient.Id,
            initiator.Id,
            analysis.RecipientPerspective,
            messages,
            savedMemories,
            memoryErrors);

        if (memoryErrors.Count > 0)
        {
            return CreateResult(
                SessionPostProcessingStatus.MemoryPersistencePartialFailure,
                relationshipChanges,
                savedMemories,
                analysis,
                $"关系变化已经保存，但部分方向记忆未保存：{string.Join(" ", memoryErrors)}");
        }

        return CreateResult(
            analysis.UsedFallback
                ? SessionPostProcessingStatus.SuccessWithFallback
                : SessionPostProcessingStatus.Success,
            relationshipChanges,
            savedMemories,
            analysis,
            analysis.UsedFallback ? analysis.FallbackReason : string.Empty);
    }

    private void SaveDirectionMemories(
        AutonomousPrivateChatSession session,
        Guid ownerAiAccountId,
        Guid subjectAiAccountId,
        DirectionalSessionInsight insight,
        IReadOnlyList<PrivateMessage> sessionMessages,
        List<AiMemory> savedMemories,
        List<string> errors)
    {
        foreach (SessionMemoryCandidate candidate in insight.MemoryCandidates)
        {
            int? salience = MapSalience(candidate.Importance);

            if (salience is null
                || !EvidenceBelongsToSession(
                    candidate,
                    subjectAiAccountId,
                    sessionMessages))
            {
                continue;
            }

            AiMemoryOperationStatus status = _memoryService.TryCreateMemory(
                ownerAiAccountId,
                subjectAiAccountId,
                candidate.Type,
                candidate.Summary,
                salience.Value,
                session.PrivateChatId,
                session.Id,
                session.EndedAt ?? session.LastActivityAt,
                out AiMemory? memory,
                out string errorMessage);

            if (status is (AiMemoryOperationStatus.Success
                    or AiMemoryOperationStatus.AlreadyExists)
                && memory is not null)
            {
                if (savedMemories.All(item => item.Id != memory.Id))
                {
                    savedMemories.Add(memory);
                }

                continue;
            }

            errors.Add(errorMessage);
        }
    }

    private static bool EvidenceBelongsToSession(
        SessionMemoryCandidate candidate,
        Guid subjectAiAccountId,
        IReadOnlyList<PrivateMessage> sessionMessages)
    {
        if (candidate.EvidenceMessageIds.Count == 0)
        {
            return false;
        }

        Dictionary<Guid, PrivateMessage> messagesById = sessionMessages
            .ToDictionary(message => message.Id);

        if (candidate.EvidenceMessageIds.Any(
                messageId => !messagesById.ContainsKey(messageId)))
        {
            return false;
        }

        if (candidate.Type is AiMemoryType.ImportantEvent
            or AiMemoryType.SharedExperience)
        {
            return true;
        }

        return candidate.EvidenceMessageIds.Any(messageId =>
            messagesById[messageId].SenderAiAccountId == subjectAiAccountId);
    }

    private static int? MapSalience(SessionMemoryImportance importance)
    {
        return importance switch
        {
            SessionMemoryImportance.Medium => MediumMemorySalience,
            SessionMemoryImportance.High => HighMemorySalience,
            _ => null
        };
    }

    private static SessionPostProcessingResult CreateResult(
        SessionPostProcessingStatus status,
        IReadOnlyList<AiRelationshipChange>? relationshipChanges = null,
        IReadOnlyList<AiMemory>? memories = null,
        SessionInsightAnalysis? analysis = null,
        string message = "")
    {
        return new SessionPostProcessingResult
        {
            Status = status,
            Analysis = analysis,
            RelationshipChanges = relationshipChanges
                ?? Array.Empty<AiRelationshipChange>(),
            Memories = memories ?? Array.Empty<AiMemory>(),
            Message = message
        };
    }
}

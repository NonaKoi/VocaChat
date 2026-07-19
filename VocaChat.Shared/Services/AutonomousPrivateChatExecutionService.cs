using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 协调一次有限的好友自主私信：判断、规划、递减概率、多轮消息、收束和 Session 后处理。
/// </summary>
public sealed class AutonomousPrivateChatExecutionService
{
    private const int MaximumContextMemoryCount = 4;

    private readonly AutonomousPrivateChatJudge _privateChatJudge;
    private readonly AiAccountService _aiAccountService;
    private readonly PrivateChatService _privateChatService;
    private readonly AutonomousPrivateChatPlanningService _planningService;
    private readonly AutonomousPrivateChatSessionService _sessionService;
    private readonly AutonomousPrivateChatRoundPlanner _roundPlanner;
    private readonly AutonomousPrivateChatContinuationDecider _continuationDecider;
    private readonly AutonomousPrivateChatClosurePlanner _closurePlanner;
    private readonly AutonomousPrivateChatRandomSource _randomSource;
    private readonly IAiMessageGenerator _messageGenerator;
    private readonly IConversationDirector _conversationDirector;
    private readonly SessionPostProcessingService _sessionPostProcessingService;
    private readonly AiMemoryService _memoryService;

    public AutonomousPrivateChatExecutionService(
        AutonomousPrivateChatJudge privateChatJudge,
        AiAccountService aiAccountService,
        PrivateChatService privateChatService,
        AutonomousPrivateChatPlanningService planningService,
        AutonomousPrivateChatSessionService sessionService,
        AutonomousPrivateChatRoundPlanner roundPlanner,
        AutonomousPrivateChatContinuationDecider continuationDecider,
        AutonomousPrivateChatClosurePlanner closurePlanner,
        AutonomousPrivateChatRandomSource randomSource,
        IAiMessageGenerator messageGenerator,
        IConversationDirector conversationDirector,
        SessionPostProcessingService sessionPostProcessingService,
        AiMemoryService memoryService)
    {
        _privateChatJudge = privateChatJudge;
        _aiAccountService = aiAccountService;
        _privateChatService = privateChatService;
        _planningService = planningService;
        _sessionService = sessionService;
        _roundPlanner = roundPlanner;
        _continuationDecider = continuationDecider;
        _closurePlanner = closurePlanner;
        _randomSource = randomSource;
        _messageGenerator = messageGenerator;
        _conversationDirector = conversationDirector;
        _sessionPostProcessingService = sessionPostProcessingService;
        _memoryService = memoryService
            ?? throw new ArgumentNullException(nameof(memoryService));
    }

    /// <summary>
    /// 对指定好友组合执行一次有限自主私信；已经保存的消息不会因后续失败回滚。
    /// </summary>
    public async Task<AutonomousPrivateChatExecutionResult> ExecuteAsync(
        Guid firstAiAccountId,
        Guid secondAiAccountId,
        DateTime evaluatedAt,
        string? requestedTopic = null,
        CancellationToken cancellationToken = default)
    {
        AutonomousPrivateChatDecision decision = _privateChatJudge.Evaluate(
            firstAiAccountId,
            secondAiAccountId,
            evaluatedAt,
            _randomSource.NextJudgeJitter());

        if (!decision.IsApproved)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.DecisionRejected,
                decision);
        }

        AiAccount? initiator = decision.InitiatorAiAccountId is Guid initiatorId
            ? _aiAccountService.FindById(initiatorId)
            : null;
        AiAccount? recipient = decision.RecipientAiAccountId is Guid recipientId
            ? _aiAccountService.FindById(recipientId)
            : null;

        if (initiator is null || recipient is null)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.ChatCreationFailed,
                decision,
                errorMessage: "判断通过后未能读取完整的好友资料。");
        }

        if (!_planningService.TryCreatePlan(
                initiator,
                recipient,
                requestedTopic,
                out AutonomousPrivateChatPlan? plan,
                out string planningError)
            || plan is null)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.PlanningFailed,
                decision,
                errorMessage: planningError);
        }

        if (!_privateChatService.TryGetOrCreateAiPrivateChat(
                initiator.Id,
                recipient.Id,
                out PrivateChat? privateChat,
                out bool privateChatCreated,
                out string chatError)
            || privateChat is null)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.ChatCreationFailed,
                decision,
                errorMessage: chatError);
        }

        if (!_sessionService.TryStartSession(
                privateChat.Id,
                initiator.Id,
                recipient.Id,
                plan.Topic,
                plan.MaximumRounds,
                plan.ContinuationRatePercent,
                evaluatedAt,
                out AutonomousPrivateChatSession? session,
                out string sessionError)
            || session is null)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.SessionCreationFailed,
                decision,
                privateChat,
                privateChatCreated,
                errorMessage: sessionError);
        }

        AutonomousPrivateChatMemoryContext memoryContext =
            LoadMemoryContext(initiator, recipient);

        List<AutonomousPrivateChatRound> rounds = new();
        List<PrivateMessage> messages = new();
        DateTime messageTime = evaluatedAt;
        double currentOccurrenceProbability = 1;
        double? currentRandomRoll = null;
        AutonomousPrivateChatRoundPlan? previousRoundPlan = null;
        string lastMessageContent = string.Empty;
        AutonomousPrivateChatSessionEndReason completionReason =
            AutonomousPrivateChatSessionEndReason.HardLimitReached;

        while (session.CompletedRounds < session.MaximumRounds)
        {
            AutonomousPrivateChatRoundPlan roundPlan = _roundPlanner.Plan(
                plan,
                _randomSource.NextUnit(),
                _randomSource.NextUnit(),
                _randomSource.NextUnit(),
                _randomSource.NextUnit());
            int roundNumber = session.CompletedRounds + 1;

            RoundExecutionAttempt roundAttempt = await TryExecuteRoundAsync(
                    session,
                    initiator,
                    recipient,
                    plan.Topic,
                    plan.InitiatorToRecipientRelationshipScore,
                    plan.RecipientToInitiatorRelationshipScore,
                    memoryContext,
                    roundPlan,
                    isClosing: false,
                    currentOccurrenceProbability,
                    currentRandomRoll,
                    roundNumber,
                    messageTime,
                    rounds,
                    messages,
                    cancellationToken);
            messageTime = roundAttempt.MessageTime;

            if (!roundAttempt.Succeeded)
            {
                return FailSessionAndCreateResult(
                    roundAttempt.FailureReason == AutonomousPrivateChatSessionEndReason.GenerationFailed
                        ? AutonomousPrivateChatExecutionStatus.GenerationFailed
                        : AutonomousPrivateChatExecutionStatus.MessagePersistenceFailed,
                    roundAttempt.FailureReason,
                    decision,
                    privateChat,
                    privateChatCreated,
                    roundAttempt.ProgressedSession ?? session,
                    rounds,
                    messages,
                    messageTime,
                    roundAttempt.ErrorMessage);
            }

            session = roundAttempt.ProgressedSession ?? session;
            previousRoundPlan = roundPlan;
            lastMessageContent = messages[^1].Content;

            if (session.CompletedRounds >= session.MaximumRounds)
            {
                completionReason =
                    AutonomousPrivateChatSessionEndReason.HardLimitReached;
                break;
            }

            bool naturallyClosed =
                _closurePlanner.LooksNaturallyClosed(lastMessageContent);
            AutonomousPrivateChatContinuationDecision continuationDecision =
                _continuationDecider.Decide(
                    plan,
                    currentOccurrenceProbability,
                    roundPlan,
                    naturallyClosed,
                    _randomSource.NextUnit());

            if (!continuationDecision.ShouldContinue)
            {
                completionReason = naturallyClosed
                    ? AutonomousPrivateChatSessionEndReason.NaturalConclusion
                    : AutonomousPrivateChatSessionEndReason
                        .ContinuationProbabilityDeclined;
                break;
            }

            currentOccurrenceProbability =
                continuationDecision.OccurrenceProbability;
            currentRandomRoll = continuationDecision.RandomRoll;
        }

        if (previousRoundPlan is null)
        {
            return FailSessionAndCreateResult(
                AutonomousPrivateChatExecutionStatus.GenerationFailed,
                AutonomousPrivateChatSessionEndReason.GenerationFailed,
                decision,
                privateChat,
                privateChatCreated,
                session,
                rounds,
                messages,
                messageTime,
                "自主私信没有生成第一轮计划。");
        }

        AutonomousPrivateChatRoundPlan closingPlan = _closurePlanner.Plan(
            plan,
            previousRoundPlan,
            lastMessageContent,
            _randomSource.NextUnit(),
            _randomSource.NextUnit(),
            _randomSource.NextUnit());

        RoundExecutionAttempt closingAttempt = await TryExecuteRoundAsync(
                session,
                initiator,
                recipient,
                plan.Topic,
                plan.InitiatorToRecipientRelationshipScore,
                plan.RecipientToInitiatorRelationshipScore,
                memoryContext,
                closingPlan,
                isClosing: true,
                occurrenceProbability: null,
                randomRoll: null,
                session.CompletedRounds + 1,
                messageTime,
                rounds,
                messages,
                cancellationToken);
        messageTime = closingAttempt.MessageTime;

        if (!closingAttempt.Succeeded)
        {
            return FailSessionAndCreateResult(
                closingAttempt.FailureReason == AutonomousPrivateChatSessionEndReason.GenerationFailed
                    ? AutonomousPrivateChatExecutionStatus.GenerationFailed
                    : AutonomousPrivateChatExecutionStatus.MessagePersistenceFailed,
                closingAttempt.FailureReason,
                decision,
                privateChat,
                privateChatCreated,
                closingAttempt.ProgressedSession ?? session,
                rounds,
                messages,
                messageTime,
                closingAttempt.ErrorMessage);
        }

        session = closingAttempt.ProgressedSession ?? session;
        if (!_sessionService.TryCompleteSession(
                session.Id,
                completionReason,
                messageTime,
                out AutonomousPrivateChatSession? completedSession,
                out string completionError))
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.SessionFinalizationFailed,
                decision,
                privateChat,
                privateChatCreated,
                completedSession ?? session,
                rounds,
                messages,
                $"消息已经保存，但自主私信状态更新失败。{completionError}");
        }

        SessionPostProcessingResult postProcessingResult =
            await _sessionPostProcessingService.ProcessAsync(
                completedSession!.Id,
                cancellationToken);

        if (postProcessingResult.Status is
            SessionPostProcessingStatus.SessionNotFound
            or SessionPostProcessingStatus.SessionNotEligible
            or SessionPostProcessingStatus.ParticipantNotFound
            or SessionPostProcessingStatus.RelationshipPersistenceFailed)
        {
            return CreateResult(
                AutonomousPrivateChatExecutionStatus.RelationshipEvolutionFailed,
                decision,
                privateChat,
                privateChatCreated,
                completedSession,
                rounds,
                messages,
                $"消息和自主私信 Session 已经保存，但后处理失败。{postProcessingResult.Message}");
        }

        return CreateResult(
            AutonomousPrivateChatExecutionStatus.Completed,
            decision,
            privateChat,
            privateChatCreated,
            completedSession,
            rounds,
            messages,
            postProcessingResult.Status ==
                    SessionPostProcessingStatus.MemoryPersistencePartialFailure
                ? postProcessingResult.Message
                : string.Empty);
    }

    private async Task<RoundExecutionAttempt> TryExecuteRoundAsync(
        AutonomousPrivateChatSession session,
        AiAccount initiator,
        AiAccount recipient,
        string topic,
        double initiatorToRecipientRelationshipScore,
        double recipientToInitiatorRelationshipScore,
        AutonomousPrivateChatMemoryContext memoryContext,
        AutonomousPrivateChatRoundPlan roundPlan,
        bool isClosing,
        double? occurrenceProbability,
        double? randomRoll,
        int roundNumber,
        DateTime messageTime,
        List<AutonomousPrivateChatRound> rounds,
        List<PrivateMessage> messages,
        CancellationToken cancellationToken)
    {
        AutonomousPrivateChatSession? progressedSession = session;
        AutonomousPrivateChatSessionEndReason failureReason =
            AutonomousPrivateChatSessionEndReason.MessagePersistenceFailed;

        if (!_sessionService.TryStartRound(
                session.Id,
                isClosing,
                occurrenceProbability,
                randomRoll,
                roundPlan.InitiatorMessageMode,
                roundPlan.RecipientMessageMode,
                roundPlan.InitiatorMessageCount,
                roundPlan.RecipientMessageCount,
                messageTime,
                out AutonomousPrivateChatRound? round,
                out string errorMessage)
            || round is null)
        {
            return RoundExecutionAttempt.Failed(
                progressedSession,
                failureReason,
                errorMessage,
                messageTime);
        }

        rounds.Add(round);
        IReadOnlyList<string> initiatorContents;

        try
        {
            initiatorContents = await GenerateAutonomousMessagesAsync(
                session,
                initiator,
                recipient,
                topic,
                roundPlan.InitiatorMessageCount,
                roundNumber,
                isInitiator: true,
                isClosing,
                initiatorToRecipientRelationshipScore,
                recipientToInitiatorRelationshipScore,
                memoryContext.InitiatorMemories,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failureReason = AutonomousPrivateChatSessionEndReason.GenerationFailed;
            errorMessage = $"发起方消息生成失败：{exception.Message}";
            return RoundExecutionAttempt.Failed(
                progressedSession,
                failureReason,
                errorMessage,
                messageTime);
        }

        if (!TrySaveMessages(
                round.Id,
                initiator,
                initiatorContents,
                ref messageTime,
                messages,
                out progressedSession,
                out errorMessage))
        {
            return RoundExecutionAttempt.Failed(
                progressedSession,
                failureReason,
                errorMessage,
                messageTime);
        }

        IReadOnlyList<string> recipientContents;

        try
        {
            recipientContents = await GenerateAutonomousMessagesAsync(
                session,
                recipient,
                initiator,
                topic,
                roundPlan.RecipientMessageCount,
                roundNumber,
                isInitiator: false,
                isClosing,
                recipientToInitiatorRelationshipScore,
                initiatorToRecipientRelationshipScore,
                memoryContext.RecipientMemories,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failureReason = AutonomousPrivateChatSessionEndReason.GenerationFailed;
            errorMessage = $"接收方消息生成失败：{exception.Message}";
            return RoundExecutionAttempt.Failed(
                progressedSession,
                failureReason,
                errorMessage,
                messageTime);
        }

        if (!TrySaveMessages(
                round.Id,
                recipient,
                recipientContents,
                ref messageTime,
                messages,
                out progressedSession,
                out errorMessage))
        {
            return RoundExecutionAttempt.Failed(
                progressedSession,
                failureReason,
                errorMessage,
                messageTime);
        }

        if (!_sessionService.TryCompleteRound(
                round.Id,
                messageTime,
                out progressedSession,
                out errorMessage))
        {
            return RoundExecutionAttempt.Failed(
                progressedSession,
                failureReason,
                errorMessage,
                messageTime);
        }

        return RoundExecutionAttempt.Completed(progressedSession, messageTime);
    }

    private async Task<IReadOnlyList<string>> GenerateAutonomousMessagesAsync(
        AutonomousPrivateChatSession session,
        AiAccount speaker,
        AiAccount otherParticipant,
        string topic,
        int messageCount,
        int roundNumber,
        bool isInitiator,
        bool isClosing,
        double speakerToOtherRelationshipScore,
        double otherToSpeakerRelationshipScore,
        IReadOnlyList<AiConversationMemory> relevantMemories,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PrivateMessage> recentHistory = _privateChatService
            .GetOrderedChatHistory(session.PrivateChatId)
            .TakeLast(12)
            .ToList();
        IReadOnlyList<AiDialogueMessage> recentMessages = recentHistory
            .Select(message => new AiDialogueMessage(
                message.SenderDisplayName,
                message.Content,
                message.SenderType,
                message.SenderAiAccountId,
                message.Id,
                message.SentAt))
            .ToList()
            .AsReadOnly();
        PrivateMessage? latestSessionMessage = recentHistory.LastOrDefault(
            message => message.AutonomousPrivateChatSessionId == session.Id);
        PrivateMessage? latestOtherMessage = recentHistory.LastOrDefault(
            message => message.AutonomousPrivateChatSessionId == session.Id
                && message.SenderAiAccountId == otherParticipant.Id);
        AiDialogueReplyTarget replyTarget = CreateAutonomousReplyTarget(
            latestSessionMessage,
            latestOtherMessage,
            speaker,
            roundNumber,
            isInitiator,
            isClosing);

        AiMessageGenerationRequest generationRequest = new()
        {
            Scenario = isClosing
                ? AiMessageGenerationScenario.AutonomousPrivateChatClosing
                : AiMessageGenerationScenario.AutonomousPrivateChat,
            Speaker = speaker,
            OtherParticipants = new[] { otherParticipant },
            Topic = topic,
            FocusContent = replyTarget.Message?.Content ?? topic,
            ReplyTarget = replyTarget,
            RecentMessages = recentMessages,
            ExpectedMessageCount = messageCount,
            RoundNumber = roundNumber,
            IsInitiator = isInitiator,
            OtherParticipantHasResponded = latestOtherMessage is not null,
            SpeakerToOtherRelationshipScore = speakerToOtherRelationshipScore,
            OtherToSpeakerRelationshipScore = otherToSpeakerRelationshipScore,
            RelevantMemories = relevantMemories
        };
        ConversationDirectionPlan directionPlan =
            await _conversationDirector.CreatePlanAsync(
                generationRequest,
                cancellationToken);
        generationRequest = generationRequest with
        {
            DirectionPlan = directionPlan,
            ActionPlan = directionPlan.ActionPlan
        };

        return await _messageGenerator.GenerateMessagesAsync(
            generationRequest,
            cancellationToken);
    }

    /// <summary>
    /// 在 Session 开始时冻结两个方向的少量记忆，避免每一轮重复查询或中途改变上下文。
    /// </summary>
    private AutonomousPrivateChatMemoryContext LoadMemoryContext(
        AiAccount initiator,
        AiAccount recipient)
    {
        return new AutonomousPrivateChatMemoryContext(
            LoadDirectionalMemories(initiator, recipient),
            LoadDirectionalMemories(recipient, initiator));
    }

    private IReadOnlyList<AiConversationMemory> LoadDirectionalMemories(
        AiAccount owner,
        AiAccount subject)
    {
        AiMemoryOperationStatus status = _memoryService.TryGetActiveMemories(
            owner.Id,
            subject.Id,
            MaximumContextMemoryCount,
            type: null,
            out IReadOnlyList<AiMemory> memories,
            out _);

        if (status != AiMemoryOperationStatus.Success)
        {
            return Array.Empty<AiConversationMemory>();
        }

        return memories
            .Select(memory => new AiConversationMemory(
                memory.OwnerAiAccountId,
                memory.SubjectAiAccountId,
                subject.Nickname,
                memory.Type,
                memory.Summary,
                memory.OccurredAt))
            .ToList()
            .AsReadOnly();
    }

    private static AiDialogueReplyTarget CreateAutonomousReplyTarget(
        PrivateMessage? latestSessionMessage,
        PrivateMessage? latestOtherMessage,
        AiAccount speaker,
        int roundNumber,
        bool isInitiator,
        bool isClosing)
    {
        if (isClosing)
        {
            return AiDialogueReplyTarget.CloseConversation(
                latestOtherMessage is null
                    ? null
                    : ToDialogueMessage(latestOtherMessage));
        }

        if (roundNumber == 1 && isInitiator)
        {
            return AiDialogueReplyTarget.OpenTopic();
        }

        if (latestSessionMessage?.SenderAiAccountId == speaker.Id)
        {
            return AiDialogueReplyTarget.ContinueTopic();
        }

        return latestOtherMessage is null
            ? AiDialogueReplyTarget.ContinueTopic()
            : AiDialogueReplyTarget.ReplyTo(
                ToDialogueMessage(latestOtherMessage));
    }

    private static AiDialogueMessage ToDialogueMessage(
        PrivateMessage message) =>
        new(
            message.SenderDisplayName,
            message.Content,
            message.SenderType,
            message.SenderAiAccountId,
            message.Id,
            message.SentAt);

    private bool TrySaveMessages(
        Guid roundId,
        AiAccount sender,
        IReadOnlyList<string> contents,
        ref DateTime messageTime,
        List<PrivateMessage> messages,
        out AutonomousPrivateChatSession? session,
        out string errorMessage)
    {
        session = null;

        foreach (string content in contents)
        {
            messageTime = messageTime.AddTicks(1);
            if (!_sessionService.TryAppendMessage(
                    roundId,
                    sender,
                    content,
                    messageTime,
                    out PrivateMessage? message,
                    out session,
                    out errorMessage)
                || message is null)
            {
                return false;
            }

            messages.Add(message);
        }

        errorMessage = string.Empty;
        return true;
    }

    private AutonomousPrivateChatExecutionResult FailSessionAndCreateResult(
        AutonomousPrivateChatExecutionStatus status,
        AutonomousPrivateChatSessionEndReason endReason,
        AutonomousPrivateChatDecision decision,
        PrivateChat privateChat,
        bool privateChatCreated,
        AutonomousPrivateChatSession session,
        IReadOnlyList<AutonomousPrivateChatRound> rounds,
        IReadOnlyList<PrivateMessage> messages,
        DateTime failedAt,
        string primaryError)
    {
        _sessionService.TryFailSession(
            session.Id,
            endReason,
            failedAt,
            out AutonomousPrivateChatSession? failedSession,
            out string finalizationError);

        return CreateResult(
            status,
            decision,
            privateChat,
            privateChatCreated,
            failedSession ?? session,
            rounds,
            messages,
            CombineErrors(primaryError, finalizationError));
    }

    private static AutonomousPrivateChatExecutionResult CreateResult(
        AutonomousPrivateChatExecutionStatus status,
        AutonomousPrivateChatDecision decision,
        PrivateChat? privateChat = null,
        bool privateChatCreated = false,
        AutonomousPrivateChatSession? session = null,
        IReadOnlyList<AutonomousPrivateChatRound>? rounds = null,
        IReadOnlyList<PrivateMessage>? messages = null,
        string errorMessage = "")
    {
        return new AutonomousPrivateChatExecutionResult
        {
            Status = status,
            Decision = decision,
            PrivateChat = privateChat,
            PrivateChatCreated = privateChatCreated,
            Session = session,
            Rounds = rounds ?? Array.Empty<AutonomousPrivateChatRound>(),
            Messages = messages ?? Array.Empty<PrivateMessage>(),
            ErrorMessage = errorMessage
        };
    }

    private static string CombineErrors(
        string primaryError,
        string secondaryError)
    {
        return string.IsNullOrWhiteSpace(secondaryError)
            ? primaryError
            : $"{primaryError} {secondaryError}";
    }

    private sealed record RoundExecutionAttempt(
        bool Succeeded,
        AutonomousPrivateChatSession? ProgressedSession,
        AutonomousPrivateChatSessionEndReason FailureReason,
        string ErrorMessage,
        DateTime MessageTime)
    {
        public static RoundExecutionAttempt Completed(
            AutonomousPrivateChatSession? session,
            DateTime messageTime) =>
            new(
                true,
                session,
                AutonomousPrivateChatSessionEndReason.HardLimitReached,
                string.Empty,
                messageTime);

        public static RoundExecutionAttempt Failed(
            AutonomousPrivateChatSession? session,
            AutonomousPrivateChatSessionEndReason failureReason,
            string errorMessage,
            DateTime messageTime) =>
            new(false, session, failureReason, errorMessage, messageTime);
    }

    private sealed record AutonomousPrivateChatMemoryContext(
        IReadOnlyList<AiConversationMemory> InitiatorMemories,
        IReadOnlyList<AiConversationMemory> RecipientMemories);
}

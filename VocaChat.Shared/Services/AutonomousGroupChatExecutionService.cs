using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 协调一次受控的多轮自主好友群聊，并在普通轮停止后执行一次自然收束。
/// </summary>
public sealed class AutonomousGroupChatExecutionService
{
    private readonly AutonomousGroupChatJudge _judge;
    private readonly AutonomousGroupChatPlanningService _planningService;
    private readonly AutonomousGroupChatRoundPlanner _roundPlanner;
    private readonly AutonomousGroupChatContinuationDecider _continuationDecider;
    private readonly AutonomousGroupChatClosurePlanner _closurePlanner;
    private readonly AutonomousGroupChatRandomSource _randomSource;
    private readonly AutonomousGroupChatSessionService _sessionService;
    private readonly AiAccountService _aiAccountService;
    private readonly GroupChatService _groupChatService;
    private readonly GroupMessageService _groupMessageService;
    private readonly GroupChatReplyPlanner _replyPlanner;
    private readonly IGroupConversationDirector _groupConversationDirector;
    private readonly GroupConversationPlanValidator _groupPlanValidator;
    private readonly IConversationDirector _conversationDirector;
    private readonly IAiMessageGenerator _messageGenerator;
    private readonly AiReplyTimingScheduler _replyTimingScheduler;
    private readonly ConversationQuestionPolicyService _questionPolicyService;
    private readonly AiIdentityContinuityService _identityContinuityService;
    private readonly AiReplyMessageCountSettingsResolver
        _replyMessageCountSettingsResolver;
    private readonly GroupConversationContextService _conversationContextService;
    private readonly GroupConversationDensitySettingsResolver _densityResolver;
    private readonly GroupConversationDiagnosticService _groupDiagnosticService;

    public AutonomousGroupChatExecutionService(
        AutonomousGroupChatJudge judge,
        AutonomousGroupChatPlanningService planningService,
        AutonomousGroupChatRoundPlanner roundPlanner,
        AutonomousGroupChatContinuationDecider continuationDecider,
        AutonomousGroupChatClosurePlanner closurePlanner,
        AutonomousGroupChatRandomSource randomSource,
        AutonomousGroupChatSessionService sessionService,
        AiAccountService aiAccountService,
        GroupChatService groupChatService,
        GroupMessageService groupMessageService,
        GroupChatReplyPlanner replyPlanner,
        IGroupConversationDirector groupConversationDirector,
        GroupConversationPlanValidator groupPlanValidator,
        IConversationDirector conversationDirector,
        IAiMessageGenerator messageGenerator,
        AiReplyTimingScheduler replyTimingScheduler,
        ConversationQuestionPolicyService questionPolicyService,
        AiIdentityContinuityService identityContinuityService,
        AiReplyMessageCountSettingsResolver replyMessageCountSettingsResolver,
        GroupConversationContextService conversationContextService,
        GroupConversationDensitySettingsResolver densityResolver,
        GroupConversationDiagnosticService groupDiagnosticService)
    {
        _judge = judge;
        _planningService = planningService;
        _roundPlanner = roundPlanner;
        _continuationDecider = continuationDecider;
        _closurePlanner = closurePlanner;
        _randomSource = randomSource;
        _sessionService = sessionService;
        _aiAccountService = aiAccountService;
        _groupChatService = groupChatService;
        _groupMessageService = groupMessageService;
        _replyPlanner = replyPlanner
            ?? throw new ArgumentNullException(nameof(replyPlanner));
        _groupConversationDirector = groupConversationDirector
            ?? throw new ArgumentNullException(
                nameof(groupConversationDirector));
        _groupPlanValidator = groupPlanValidator
            ?? throw new ArgumentNullException(nameof(groupPlanValidator));
        _conversationDirector = conversationDirector;
        _messageGenerator = messageGenerator;
        _replyTimingScheduler = replyTimingScheduler
            ?? throw new ArgumentNullException(nameof(replyTimingScheduler));
        _questionPolicyService = questionPolicyService
            ?? throw new ArgumentNullException(nameof(questionPolicyService));
        _identityContinuityService = identityContinuityService
            ?? throw new ArgumentNullException(nameof(identityContinuityService));
        _replyMessageCountSettingsResolver = replyMessageCountSettingsResolver
            ?? throw new ArgumentNullException(
                nameof(replyMessageCountSettingsResolver));
        _conversationContextService = conversationContextService
            ?? throw new ArgumentNullException(
                nameof(conversationContextService));
        _densityResolver = densityResolver
            ?? throw new ArgumentNullException(nameof(densityResolver));
        _groupDiagnosticService = groupDiagnosticService
            ?? throw new ArgumentNullException(nameof(groupDiagnosticService));
    }

    public async Task<AutonomousGroupChatExecutionResult> ExecuteAsync(
        IEnumerable<Guid> participantAiAccountIds,
        DateTime evaluatedAt,
        double randomJitter,
        string? requestedTopic = null,
        CancellationToken cancellationToken = default)
    {
        AutonomousGroupChatDecision decision = _judge.Evaluate(
            participantAiAccountIds,
            randomJitter);
        if (!decision.IsApproved)
        {
            return CreateResult(
                AutonomousGroupChatExecutionStatus.DecisionRejected,
                decision);
        }

        if (!_planningService.TryCreatePlan(
                decision,
                requestedTopic,
                out AutonomousGroupChatPlan? plan,
                out string planningError)
            || plan is null)
        {
            return CreateResult(
                AutonomousGroupChatExecutionStatus.PlanningFailed,
                decision,
                errorMessage: planningError);
        }

        IReadOnlyList<AiAccount> participants = plan.MemberAiAccountIds
            .Select(_aiAccountService.FindById)
            .Where(account => account is not null)
            .Cast<AiAccount>()
            .ToList()
            .AsReadOnly();
        if (participants.Count != plan.MemberAiAccountIds.Count)
        {
            return CreateResult(
                AutonomousGroupChatExecutionStatus.ParticipantUnavailable,
                decision,
                errorMessage: "判断通过后未能读取完整的好友资料。");
        }

        string groupName = CreateGroupName(participants);
        if (!_groupChatService.TryGetOrCreateFriendGroupChat(
                groupName,
                plan.MemberAiAccountIds,
                out GroupChat? groupChat,
                out bool groupChatCreated,
                out string groupChatError)
            || groupChat is null)
        {
            return CreateResult(
                AutonomousGroupChatExecutionStatus.GroupChatCreationFailed,
                decision,
                errorMessage: groupChatError);
        }

        if (!_sessionService.TryStartSession(
                groupChat.Id,
                plan.InitiatorAiAccountId,
                plan.MemberAiAccountIds,
                plan.Topic,
                plan.MaximumRounds,
                plan.ContinuationRatePercent,
                evaluatedAt,
                out AutonomousGroupChatSession? session,
                out string sessionError)
            || session is null)
        {
            return CreateResult(
                AutonomousGroupChatExecutionStatus.SessionCreationFailed,
                decision,
                groupChat,
                groupChatCreated,
                errorMessage: sessionError);
        }

        GroupConversationDensitySettings densitySettings =
            _densityResolver.Resolve();

        List<AutonomousGroupChatRound> rounds = new();
        List<GroupMessage> messages = new();
        DateTime messageTime = evaluatedAt;
        double currentOccurrenceProbability = 1;
        double? currentRandomRoll = null;
        AutonomousGroupChatRoundPlan? previousRoundPlan = null;
        string lastMessageContent = string.Empty;
        Guid? latestSpeakerId = null;
        AutonomousGroupChatSessionEndReason completionReason =
            AutonomousGroupChatSessionEndReason.HardLimitReached;

        while (session.CompletedRounds < session.MaximumRounds)
        {
            int roundNumber = session.CompletedRounds + 1;
            IReadOnlyCollection<Guid> previousSpeakerIds = previousRoundPlan?
                .Speakers
                .Select(item => item.SpeakerAiAccountId)
                .ToHashSet()
                ?? new HashSet<Guid>();
            AutonomousGroupChatRoundPlan fallbackRoundPlan = _roundPlanner.Plan(
                plan,
                roundNumber,
                previousSpeakerIds,
                latestSpeakerId,
                _randomSource.NextUnit(),
                NextRolls(3),
                NextRolls(3));
            Guid interactionBatchId = Guid.NewGuid();
            GroupConversationTurnPlan turnPlan;
            try
            {
                turnPlan = await CreateValidatedGroupPlanAsync(
                    CreatePlanningRequest(
                        groupChat,
                        plan,
                        session,
                        roundNumber == 1
                            ? GroupConversationPlanningScenario
                                .AutonomousOpening
                            : GroupConversationPlanningScenario
                                .AutonomousContinuation,
                        fallbackRoundPlan,
                        densitySettings,
                        interactionBatchId),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return FailSessionAndCreateResult(
                    AutonomousGroupChatExecutionStatus.GenerationFailed,
                    AutonomousGroupChatSessionEndReason.GenerationFailed,
                    decision,
                    groupChat,
                    groupChatCreated,
                    session,
                    rounds,
                    messages,
                    messageTime,
                    $"自主好友群聊语义规划失败：{exception.Message}");
            }

            AutonomousGroupChatRoundPlan roundPlan = ToRoundPlan(turnPlan);
            RoundExecutionAttempt roundAttempt = await TryExecuteRoundAsync(
                session,
                plan,
                participants,
                groupChat,
                roundPlan,
                turnPlan,
                interactionBatchId,
                isClosing: false,
                currentOccurrenceProbability,
                currentRandomRoll,
                roundNumber,
                messageTime,
                rounds,
                messages,
                densitySettings.MaximumMessagesPerTurn,
                cancellationToken);
            messageTime = roundAttempt.MessageTime;

            if (!roundAttempt.Succeeded)
            {
                return FailSessionAndCreateResult(
                    roundAttempt.FailureReason
                        == AutonomousGroupChatSessionEndReason.GenerationFailed
                            ? AutonomousGroupChatExecutionStatus.GenerationFailed
                            : AutonomousGroupChatExecutionStatus
                                .MessagePersistenceFailed,
                    roundAttempt.FailureReason,
                    decision,
                    groupChat,
                    groupChatCreated,
                    roundAttempt.ProgressedSession ?? session,
                    rounds,
                    messages,
                    messageTime,
                    roundAttempt.ErrorMessage);
            }

            session = roundAttempt.ProgressedSession ?? session;
            previousRoundPlan = roundPlan;
            if (messages.Count > 0)
            {
                lastMessageContent = messages[^1].Content;
                latestSpeakerId = messages[^1].SenderAiAccountId;
            }

            if (session.CompletedRounds >= session.MaximumRounds)
            {
                completionReason =
                    AutonomousGroupChatSessionEndReason.HardLimitReached;
                break;
            }

            bool naturallyClosed =
                _closurePlanner.LooksNaturallyClosed(lastMessageContent);
            AutonomousGroupChatContinuationDecision continuationDecision =
                _continuationDecider.Decide(
                    plan,
                    currentOccurrenceProbability,
                    roundPlan,
                    naturallyClosed,
                    _randomSource.NextUnit());

            if (!continuationDecision.ShouldContinue)
            {
                completionReason = naturallyClosed
                    ? AutonomousGroupChatSessionEndReason.NaturalConclusion
                    : AutonomousGroupChatSessionEndReason
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
                AutonomousGroupChatExecutionStatus.GenerationFailed,
                AutonomousGroupChatSessionEndReason.GenerationFailed,
                decision,
                groupChat,
                groupChatCreated,
                session,
                rounds,
                messages,
                messageTime,
                "自主好友群聊没有生成第一轮计划。");
        }

        AutonomousGroupChatRoundPlan closingPlan = _closurePlanner.Plan(
            plan,
            previousRoundPlan,
            lastMessageContent,
            latestSpeakerId,
            _randomSource.NextUnit(),
            NextRolls(2),
            NextRolls(2));
        Guid closingInteractionBatchId = Guid.NewGuid();
        GroupConversationTurnPlan closingTurnPlan;
        try
        {
            closingTurnPlan = await CreateValidatedGroupPlanAsync(
                CreatePlanningRequest(
                    groupChat,
                    plan,
                    session,
                    GroupConversationPlanningScenario.AutonomousClosing,
                    closingPlan,
                    densitySettings,
                    closingInteractionBatchId),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return FailSessionAndCreateResult(
                AutonomousGroupChatExecutionStatus.GenerationFailed,
                AutonomousGroupChatSessionEndReason.GenerationFailed,
                decision,
                groupChat,
                groupChatCreated,
                session,
                rounds,
                messages,
                messageTime,
                $"自主好友群聊收束规划失败：{exception.Message}");
        }

        closingPlan = ToRoundPlan(closingTurnPlan);
        RoundExecutionAttempt closingAttempt = await TryExecuteRoundAsync(
            session,
            plan,
            participants,
            groupChat,
            closingPlan,
            closingTurnPlan,
            closingInteractionBatchId,
            isClosing: true,
            occurrenceProbability: null,
            randomRoll: null,
            session.CompletedRounds + 1,
            messageTime,
            rounds,
            messages,
            densitySettings.MaximumMessagesPerTurn,
            cancellationToken);
        messageTime = closingAttempt.MessageTime;

        if (!closingAttempt.Succeeded)
        {
            return FailSessionAndCreateResult(
                closingAttempt.FailureReason
                    == AutonomousGroupChatSessionEndReason.GenerationFailed
                        ? AutonomousGroupChatExecutionStatus.GenerationFailed
                        : AutonomousGroupChatExecutionStatus
                            .MessagePersistenceFailed,
                closingAttempt.FailureReason,
                decision,
                groupChat,
                groupChatCreated,
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
                out AutonomousGroupChatSession? completedSession,
                out string completionError))
        {
            return CreateResult(
                AutonomousGroupChatExecutionStatus.SessionFinalizationFailed,
                decision,
                groupChat,
                groupChatCreated,
                completedSession ?? session,
                rounds,
                messages,
                $"群消息已经保存，但 Session 状态更新失败。{completionError}");
        }

        return CreateResult(
            AutonomousGroupChatExecutionStatus.Completed,
            decision,
            groupChat,
            groupChatCreated,
            completedSession,
            rounds,
            messages);
    }

    private async Task<RoundExecutionAttempt> TryExecuteRoundAsync(
        AutonomousGroupChatSession session,
        AutonomousGroupChatPlan plan,
        IReadOnlyList<AiAccount> participants,
        GroupChat groupChat,
        AutonomousGroupChatRoundPlan roundPlan,
        GroupConversationTurnPlan turnPlan,
        Guid interactionBatchId,
        bool isClosing,
        double? occurrenceProbability,
        double? randomRoll,
        int roundNumber,
        DateTime messageTime,
        List<AutonomousGroupChatRound> rounds,
        List<GroupMessage> messages,
        int maximumTotalMessageCount,
        CancellationToken cancellationToken)
    {
        if (!_sessionService.TryStartRound(
                session.Id,
                isClosing,
                occurrenceProbability,
                randomRoll,
                roundPlan.Speakers.Count,
                roundPlan.PlannedMessageCount,
                messageTime,
                out AutonomousGroupChatRound? round,
                out string errorMessage)
            || round is null)
        {
            _groupDiagnosticService.RecordFailure(
                AiMessageGenerationScenario.AutonomousGroupChat,
                groupChat.Id,
                null,
                "轮次保存",
                errorMessage,
                messages.Count > 0);
            return RoundExecutionAttempt.Failed(
                session,
                AutonomousGroupChatSessionEndReason.MessagePersistenceFailed,
                errorMessage,
                messageTime);
        }

        rounds.Add(round);
        AutonomousGroupChatSession? progressedSession = session;

        int savedRoundMessageCount = 0;
        for (int speakerIndex = 0;
             speakerIndex < roundPlan.Speakers.Count;
             speakerIndex++)
        {
            AutonomousGroupChatSpeakerPlan speakerPlan =
                roundPlan.Speakers[speakerIndex];
            AiAccount speaker = participants.Single(account =>
                account.Id == speakerPlan.SpeakerAiAccountId);
            GroupConversationSpeakerPlan semanticSpeakerPlan =
                turnPlan.Speakers.Single(item =>
                    item.SpeakerAiAccountId == speaker.Id);
            Guid aiResponseBatchId = Guid.NewGuid();
            AiDirectedMessageBatch batch;

            try
            {
                batch = await GenerateMessagesAsync(
                    session,
                    groupChat,
                    plan,
                    participants,
                    speaker,
                    semanticSpeakerPlan,
                    interactionBatchId,
                    aiResponseBatchId,
                    roundNumber,
                    isClosing,
                    ApplyMessageBudget(
                        _replyMessageCountSettingsResolver.Resolve(speaker.Id),
                        maximumTotalMessageCount,
                        savedRoundMessageCount,
                        roundPlan.Speakers.Count - speakerIndex - 1),
                    cancellationToken);

                if (batch.Contents.Count != batch.Request.ExpectedMessageCount)
                {
                    throw new InvalidOperationException(
                        $"计划生成 {batch.Request.ExpectedMessageCount} 条消息，实际返回 {batch.Contents.Count} 条。");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _groupDiagnosticService.RecordFailure(
                    AiMessageGenerationScenario.AutonomousGroupChat,
                    groupChat.Id,
                    speaker.Id,
                    "消息生成",
                    exception.Message,
                    savedRoundMessageCount > 0);
                return RoundExecutionAttempt.Failed(
                    progressedSession,
                    AutonomousGroupChatSessionEndReason.GenerationFailed,
                    $"{speaker.Nickname} 的群消息生成失败：{exception.Message}",
                    messageTime);
            }

            if (savedRoundMessageCount + batch.Contents.Count
                > maximumTotalMessageCount)
            {
                const string densityError =
                    "好友群聊消息超过本轮允许的消息总量。";
                _groupDiagnosticService.RecordFailure(
                    AiMessageGenerationScenario.AutonomousGroupChat,
                    groupChat.Id,
                    speaker.Id,
                    "消息数量控制",
                    densityError,
                    savedRoundMessageCount > 0);
                return RoundExecutionAttempt.Failed(
                    progressedSession,
                    AutonomousGroupChatSessionEndReason.GenerationFailed,
                    densityError,
                    messageTime);
            }

            int adjustedPlannedMessageCount = round.PlannedMessageCount
                + batch.Contents.Count
                - speakerPlan.MessageCount;
            if (!_sessionService.TryUpdateRoundPlannedMessageCount(
                    round.Id,
                    adjustedPlannedMessageCount,
                    out errorMessage))
            {
                _groupDiagnosticService.RecordFailure(
                    AiMessageGenerationScenario.AutonomousGroupChat,
                    groupChat.Id,
                    speaker.Id,
                    "轮次计划保存",
                    errorMessage,
                    savedRoundMessageCount > 0);
                return RoundExecutionAttempt.Failed(
                    progressedSession,
                    AutonomousGroupChatSessionEndReason
                        .MessagePersistenceFailed,
                    errorMessage,
                    messageTime);
            }

            round = _sessionService.FindRoundById(round.Id) ?? round;

            List<GroupMessage> savedSpeakerMessages = new();
            for (int index = 0; index < batch.Contents.Count; index++)
            {
                try
                {
                    if (index == 0)
                    {
                        await _replyTimingScheduler.WaitForReplyAsync(
                            speaker.Id,
                            messageTime,
                            cancellationToken);
                    }
                    else
                    {
                        await _replyTimingScheduler
                            .WaitForConsecutiveMessageAsync(
                                speaker.Id,
                                messageTime,
                                cancellationToken);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _groupDiagnosticService.RecordFailure(
                        AiMessageGenerationScenario.AutonomousGroupChat,
                        groupChat.Id,
                        speaker.Id,
                        "回复等待",
                        exception.Message,
                        savedRoundMessageCount > 0);
                    return RoundExecutionAttempt.Failed(
                        progressedSession,
                        AutonomousGroupChatSessionEndReason.GenerationFailed,
                        exception.Message,
                        messageTime);
                }

                messageTime = DateTime.Now;
                if (!_sessionService.TryAppendMessage(
                        round.Id,
                        speaker,
                        batch.Contents[index],
                        messageTime,
                        interactionBatchId,
                        aiResponseBatchId,
                        batch.Request.ReplyTarget?.Message?.MessageId,
                        out GroupMessage? message,
                        out progressedSession,
                        out errorMessage)
                    || message is null)
                {
                    ApplyIdentityContinuity(
                        groupChat.Id,
                        batch,
                        savedSpeakerMessages);
                    _groupDiagnosticService.RecordFailure(
                        AiMessageGenerationScenario.AutonomousGroupChat,
                        groupChat.Id,
                        speaker.Id,
                        "消息保存",
                        errorMessage,
                        savedRoundMessageCount > 0);
                    return RoundExecutionAttempt.Failed(
                        progressedSession,
                        AutonomousGroupChatSessionEndReason
                            .MessagePersistenceFailed,
                        errorMessage,
                        messageTime);
                }

                messages.Add(message);
                savedSpeakerMessages.Add(message);
                savedRoundMessageCount++;
            }

            ApplyIdentityContinuity(
                groupChat.Id,
                batch,
                savedSpeakerMessages);
        }

        if (!_sessionService.TryCompleteRound(
                round.Id,
                messageTime,
                out progressedSession,
                out errorMessage))
        {
            _groupDiagnosticService.RecordFailure(
                AiMessageGenerationScenario.AutonomousGroupChat,
                groupChat.Id,
                null,
                "轮次完成保存",
                errorMessage,
                savedRoundMessageCount > 0);
            return RoundExecutionAttempt.Failed(
                progressedSession,
                AutonomousGroupChatSessionEndReason.MessagePersistenceFailed,
                errorMessage,
                messageTime);
        }

        AutonomousGroupChatRound? completedRound =
            _sessionService.FindRoundById(round.Id);
        if (completedRound is not null)
        {
            rounds[^1] = completedRound;
        }

        return RoundExecutionAttempt.Completed(progressedSession, messageTime);
    }

    private async Task<AiDirectedMessageBatch> GenerateMessagesAsync(
        AutonomousGroupChatSession session,
        GroupChat groupChat,
        AutonomousGroupChatPlan plan,
        IReadOnlyList<AiAccount> participants,
        AiAccount speaker,
        GroupConversationSpeakerPlan speakerPlan,
        Guid interactionBatchId,
        Guid aiResponseBatchId,
        int roundNumber,
        bool isClosing,
        AiMessageCountRange allowedMessageCountRange,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GroupMessage> recentHistory = _groupMessageService
            .GetOrderedChatHistory(groupChat)
            .TakeLast(12)
            .ToList();
        IReadOnlyList<AiDialogueMessage> recentMessages = recentHistory
            .Select(ToDialogueMessage)
            .ToList()
            .AsReadOnly();
        IReadOnlyList<GroupMessage> sessionHistory = recentHistory
            .Where(message =>
                message.AutonomousGroupChatSessionId == session.Id)
            .ToList()
            .AsReadOnly();
        GroupMessage? latestSessionMessage = sessionHistory.LastOrDefault();
        GroupMessage? conversationAnchorMessage = sessionHistory
            .FirstOrDefault();
        GroupMessage? targetMessage = ResolveTargetMessage(
            speakerPlan,
            sessionHistory,
            speaker);
        AiAccount? relationshipTarget = ResolveRelationshipTarget(
            participants,
            speakerPlan,
            targetMessage,
            speaker);
        AiDialogueReplyTarget replyTarget = CreateReplyTarget(
            targetMessage,
            roundNumber,
            isClosing,
            speaker.Id == plan.InitiatorAiAccountId);
        AiDialogueMessage? conversationAnchor = conversationAnchorMessage is null
            ? null
            : ToDialogueMessage(conversationAnchorMessage);
        AiMessageGenerationRequest request = new()
        {
            Scenario = AiMessageGenerationScenario.AutonomousGroupChat,
            UsageCorrelation = new AiModelUsageCorrelation
            {
                GroupChatId = groupChat.Id,
                AutonomousGroupChatSessionId = session.Id,
                InteractionBatchId = interactionBatchId,
                AiResponseBatchId = aiResponseBatchId
            },
            Speaker = speaker,
            OtherParticipants = participants
                .Where(account => account.Id != speaker.Id)
                .OrderByDescending(account =>
                    account.Id == relationshipTarget?.Id)
                .ToList()
                .AsReadOnly(),
            PrimarySpeaker = participants.Single(account =>
                account.Id == plan.InitiatorAiAccountId),
            Topic = plan.Topic,
            FocusContent = targetMessage?.Content ?? plan.Topic,
            ReplyTarget = replyTarget,
            ConversationAnchor = conversationAnchor,
            RecentMessages = recentMessages,
            ExpectedMessageCount = 1,
            AllowedMessageCountRange = allowedMessageCountRange,
            RoundNumber = roundNumber,
            IsInitiator = speaker.Id == plan.InitiatorAiAccountId,
            OtherParticipantHasResponded = latestSessionMessage is not null
                && latestSessionMessage.SenderAiAccountId != speaker.Id,
            QuestionPolicy = _questionPolicyService.CreatePolicy(
                speaker.Id,
                recentMessages),
            GroupConversationPlan = speakerPlan
        };
        request = _conversationContextService.PrepareGenerationRequest(
            request,
            relationshipTarget);
        ConversationDirectionPlan directionPlan =
            await _conversationDirector.CreatePlanAsync(
                request,
                cancellationToken);
        AiIdentityContinuityPlan continuityPlan = _identityContinuityService
            .ValidateDirectionPlan(request, directionPlan);
        directionPlan = continuityPlan.DirectionPlan;
        request = request with
        {
            DirectionPlan = directionPlan,
            ActionPlan = directionPlan.ActionPlan,
            ExpectedMessageCount = directionPlan.SelectedMessageCount
        };

        IReadOnlyList<string> contents =
            await _messageGenerator.GenerateMessagesAsync(
                request,
                cancellationToken);
        return new AiDirectedMessageBatch(request, continuityPlan, contents);
    }

    private static GroupMessage? ResolveTargetMessage(
        GroupConversationSpeakerPlan speakerPlan,
        IReadOnlyList<GroupMessage> recentHistory,
        AiAccount speaker)
    {
        if (speakerPlan.Audience ==
                GroupConversationAudience.SpecificAiAccount
            && speakerPlan.TargetAiAccountId is Guid targetAiAccountId)
        {
            GroupMessage? targetedMessage = recentHistory.LastOrDefault(
                message => message.SenderAiAccountId == targetAiAccountId);
            if (targetedMessage is not null)
            {
                return targetedMessage;
            }
        }

        if (speakerPlan.ReplyTargetMessageId is Guid replyTargetMessageId)
        {
            GroupMessage? targetedMessage = recentHistory.LastOrDefault(
                message => message.Id == replyTargetMessageId);
            if (targetedMessage is not null)
            {
                return targetedMessage;
            }
        }

        return recentHistory.LastOrDefault(message =>
            message.AutonomousGroupChatSessionId is not null
            && message.SenderAiAccountId != speaker.Id);
    }

    private static AiAccount? ResolveRelationshipTarget(
        IReadOnlyList<AiAccount> participants,
        GroupConversationSpeakerPlan speakerPlan,
        GroupMessage? targetMessage,
        AiAccount speaker)
    {
        Guid? targetAiAccountId = targetMessage?.SenderAiAccountId
            ?? speakerPlan.TargetAiAccountId;
        return targetAiAccountId is null || targetAiAccountId == speaker.Id
            ? null
            : participants.SingleOrDefault(account =>
                account.Id == targetAiAccountId);
    }

    private static AiDialogueReplyTarget CreateReplyTarget(
        GroupMessage? targetMessage,
        int roundNumber,
        bool isClosing,
        bool isInitiator)
    {
        AiDialogueMessage? dialogueTarget = targetMessage is null
            ? null
            : ToDialogueMessage(targetMessage);

        if (isClosing)
        {
            return AiDialogueReplyTarget.CloseConversation(dialogueTarget);
        }

        if (roundNumber == 1 && isInitiator && dialogueTarget is null)
        {
            return AiDialogueReplyTarget.OpenTopic();
        }

        return dialogueTarget is null
            ? AiDialogueReplyTarget.ContinueTopic()
            : AiDialogueReplyTarget.ReplyTo(dialogueTarget);
    }

    private GroupConversationPlanningRequest CreatePlanningRequest(
        GroupChat groupChat,
        AutonomousGroupChatPlan plan,
        AutonomousGroupChatSession session,
        GroupConversationPlanningScenario scenario,
        AutonomousGroupChatRoundPlan fallbackRoundPlan,
        GroupConversationDensitySettings densitySettings,
        Guid interactionBatchId)
    {
        IReadOnlyList<GroupMessage> recentHistory = _groupMessageService
            .GetOrderedChatHistory(groupChat)
            .TakeLast(12)
            .ToList()
            .AsReadOnly();
        GroupMessage? anchorMessage = scenario ==
                GroupConversationPlanningScenario.AutonomousOpening
            ? null
            : recentHistory.LastOrDefault(message =>
                message.AutonomousGroupChatSessionId == session.Id);

        return new GroupConversationPlanningRequest
        {
            GroupChat = groupChat,
            UsageCorrelation = new AiModelUsageCorrelation
            {
                GroupChatId = groupChat.Id,
                AutonomousGroupChatSessionId = session.Id,
                InteractionBatchId = interactionBatchId
            },
            Scenario = scenario,
            AnchorMessage = anchorMessage,
            Topic = plan.Topic,
            RequiredSpeakerAiAccountId = scenario ==
                    GroupConversationPlanningScenario.AutonomousOpening
                ? plan.InitiatorAiAccountId
                : null,
            PreferredSpeakerAiAccountIds = fallbackRoundPlan.Speakers
                .Select(item => item.SpeakerAiAccountId)
                .ToList()
                .AsReadOnly(),
            RecentMessages = recentHistory,
            MaximumSpeakerCount = scenario ==
                    GroupConversationPlanningScenario.AutonomousClosing
                ? Math.Min(
                    2,
                    densitySettings.WholeGroupMaximumSpeakersPerTurn)
                : densitySettings.WholeGroupMaximumSpeakersPerTurn,
            MaximumTotalMessageCount = densitySettings.MaximumMessagesPerTurn
        };
    }

    private async Task<GroupConversationTurnPlan> CreateValidatedGroupPlanAsync(
        GroupConversationPlanningRequest request,
        CancellationToken cancellationToken)
    {
        GroupConversationTurnPlan? plan = null;
        string? rejectedModelPlanReason = null;

        try
        {
            plan = await _groupConversationDirector.CreatePlanAsync(
                request,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 模型群级导演不可用时，继续使用同一业务边界内的规则计划。
            rejectedModelPlanReason = $"模型导演调用失败：{exception.Message}";
        }

        if (_groupPlanValidator.TryValidate(
                request,
                plan,
                out string modelPlanError))
        {
            _groupDiagnosticService.RecordPlan(request, plan!);
            return plan!;
        }

        rejectedModelPlanReason ??= modelPlanError;

        RuleBasedGroupConversationDirector fallbackDirector = new(
            _replyPlanner);
        GroupConversationTurnPlan fallbackPlan;
        try
        {
            fallbackPlan = await fallbackDirector.CreatePlanAsync(
                request,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _groupDiagnosticService.RecordFailure(
                AiMessageGenerationScenario.AutonomousGroupChat,
                request.GroupChat.Id,
                null,
                "群聊规划",
                exception.Message,
                false);
            throw;
        }
        if (!_groupPlanValidator.TryValidate(
                request,
                fallbackPlan,
                out string fallbackError))
        {
            _groupDiagnosticService.RecordFailure(
                AiMessageGenerationScenario.AutonomousGroupChat,
                request.GroupChat.Id,
                null,
                "群聊规划",
                fallbackError,
                false);
            throw new AiMessageGenerationException(fallbackError);
        }

        _groupDiagnosticService.RecordPlan(
            request,
            fallbackPlan,
            rejectedModelPlanReason);
        return fallbackPlan;
    }

    private static AiMessageCountRange ApplyMessageBudget(
        AiMessageCountRange accountRange,
        int maximumTotalMessageCount,
        int savedMessageCount,
        int remainingSpeakerCount)
    {
        int currentCapacity = maximumTotalMessageCount
            - savedMessageCount
            - remainingSpeakerCount;
        int maximum = Math.Min(accountRange.Maximum, currentCapacity);
        int minimum = Math.Min(accountRange.Minimum, maximum);

        if (maximum < 1)
        {
            throw new AiMessageGenerationException(
                "好友群聊单轮消息总量不足以让已选中的好友完成发言。");
        }

        return new AiMessageCountRange(Math.Max(1, minimum), maximum);
    }

    private static AutonomousGroupChatRoundPlan ToRoundPlan(
        GroupConversationTurnPlan turnPlan)
    {
        return new AutonomousGroupChatRoundPlan
        {
            Speakers = turnPlan.Speakers
                .Select(speaker => new AutonomousGroupChatSpeakerPlan
                {
                    SpeakerAiAccountId = speaker.SpeakerAiAccountId,
                    MessageCount = 1
                })
                .ToList()
                .AsReadOnly()
        };
    }

    private void ApplyIdentityContinuity(
        Guid groupChatId,
        AiDirectedMessageBatch batch,
        IReadOnlyList<GroupMessage> savedMessages)
    {
        if (savedMessages.Count == 0)
        {
            return;
        }

        _identityContinuityService.ApplyAfterMessagesSaved(
            batch.Request,
            batch.ContinuityPlan,
            groupChatId,
            savedMessages.Select(message => new AiPersistedMessageEvidence(
                    message.Id,
                    message.Content,
                    message.SentAt))
                .ToList()
                .AsReadOnly());
    }

    private AutonomousGroupChatExecutionResult FailSessionAndCreateResult(
        AutonomousGroupChatExecutionStatus status,
        AutonomousGroupChatSessionEndReason endReason,
        AutonomousGroupChatDecision decision,
        GroupChat groupChat,
        bool groupChatCreated,
        AutonomousGroupChatSession session,
        IReadOnlyList<AutonomousGroupChatRound> rounds,
        IReadOnlyList<GroupMessage> messages,
        DateTime failedAt,
        string primaryError)
    {
        _sessionService.TryFailSession(
            session.Id,
            endReason,
            failedAt,
            out AutonomousGroupChatSession? failedSession,
            out string finalizationError);

        string errorMessage = string.IsNullOrWhiteSpace(finalizationError)
            ? primaryError
            : $"{primaryError} {finalizationError}";
        return CreateResult(
            status,
            decision,
            groupChat,
            groupChatCreated,
            failedSession ?? session,
            rounds,
            messages,
            errorMessage);
    }

    private IReadOnlyList<double> NextRolls(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => _randomSource.NextUnit())
            .ToList()
            .AsReadOnly();
    }

    private static string CreateGroupName(IReadOnlyList<AiAccount> participants)
    {
        string memberNames = participants.Count <= 3
            ? string.Join("、", participants.Select(account => account.Nickname))
            : $"{participants[0].Nickname}、{participants[1].Nickname}等人";
        string name = $"{memberNames}的群聊";
        return name.Length <= GroupChat.NameMaxLength
            ? name
            : name[..GroupChat.NameMaxLength];
    }

    private static AiDialogueMessage ToDialogueMessage(GroupMessage message) =>
        new(
            message.SenderDisplayName,
            message.Content,
            message.SenderType,
            message.SenderAiAccountId,
            message.Id,
            message.SentAt);

    private static AutonomousGroupChatExecutionResult CreateResult(
        AutonomousGroupChatExecutionStatus status,
        AutonomousGroupChatDecision decision,
        GroupChat? groupChat = null,
        bool groupChatCreated = false,
        AutonomousGroupChatSession? session = null,
        IReadOnlyList<AutonomousGroupChatRound>? rounds = null,
        IReadOnlyList<GroupMessage>? messages = null,
        string errorMessage = "")
    {
        return new AutonomousGroupChatExecutionResult
        {
            Status = status,
            Decision = decision,
            GroupChat = groupChat,
            GroupChatCreated = groupChatCreated,
            Session = session,
            Rounds = rounds ?? Array.Empty<AutonomousGroupChatRound>(),
            Messages = messages ?? Array.Empty<GroupMessage>(),
            ErrorMessage = errorMessage
        };
    }

    private sealed record RoundExecutionAttempt(
        bool Succeeded,
        AutonomousGroupChatSession? ProgressedSession,
        AutonomousGroupChatSessionEndReason FailureReason,
        string ErrorMessage,
        DateTime MessageTime)
    {
        public static RoundExecutionAttempt Completed(
            AutonomousGroupChatSession? session,
            DateTime messageTime) =>
            new(
                true,
                session,
                AutonomousGroupChatSessionEndReason.Completed,
                string.Empty,
                messageTime);

        public static RoundExecutionAttempt Failed(
            AutonomousGroupChatSession? session,
            AutonomousGroupChatSessionEndReason failureReason,
            string errorMessage,
            DateTime messageTime) =>
            new(false, session, failureReason, errorMessage, messageTime);
    }
}

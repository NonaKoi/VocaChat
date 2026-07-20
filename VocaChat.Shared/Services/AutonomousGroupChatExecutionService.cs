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
    private readonly IConversationDirector _conversationDirector;
    private readonly IAiMessageGenerator _messageGenerator;
    private readonly AiReplyTimingScheduler _replyTimingScheduler;
    private readonly ConversationQuestionPolicyService _questionPolicyService;
    private readonly AiIdentityContinuityService _identityContinuityService;

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
        IConversationDirector conversationDirector,
        IAiMessageGenerator messageGenerator,
        AiReplyTimingScheduler replyTimingScheduler,
        ConversationQuestionPolicyService questionPolicyService,
        AiIdentityContinuityService identityContinuityService)
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
        _conversationDirector = conversationDirector;
        _messageGenerator = messageGenerator;
        _replyTimingScheduler = replyTimingScheduler
            ?? throw new ArgumentNullException(nameof(replyTimingScheduler));
        _questionPolicyService = questionPolicyService
            ?? throw new ArgumentNullException(nameof(questionPolicyService));
        _identityContinuityService = identityContinuityService
            ?? throw new ArgumentNullException(nameof(identityContinuityService));
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
            AutonomousGroupChatRoundPlan roundPlan = _roundPlanner.Plan(
                plan,
                roundNumber,
                previousSpeakerIds,
                latestSpeakerId,
                _randomSource.NextUnit(),
                NextRolls(3),
                NextRolls(3));
            RoundExecutionAttempt roundAttempt = await TryExecuteRoundAsync(
                session,
                plan,
                participants,
                groupChat,
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
        RoundExecutionAttempt closingAttempt = await TryExecuteRoundAsync(
            session,
            plan,
            participants,
            groupChat,
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
        bool isClosing,
        double? occurrenceProbability,
        double? randomRoll,
        int roundNumber,
        DateTime messageTime,
        List<AutonomousGroupChatRound> rounds,
        List<GroupMessage> messages,
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
            return RoundExecutionAttempt.Failed(
                session,
                AutonomousGroupChatSessionEndReason.MessagePersistenceFailed,
                errorMessage,
                messageTime);
        }

        rounds.Add(round);
        AutonomousGroupChatSession? progressedSession = session;

        foreach (AutonomousGroupChatSpeakerPlan speakerPlan in roundPlan.Speakers)
        {
            AiAccount speaker = participants.Single(account =>
                account.Id == speakerPlan.SpeakerAiAccountId);
            AiDirectedMessageBatch batch;

            try
            {
                batch = await GenerateMessagesAsync(
                    session,
                    groupChat,
                    plan,
                    participants,
                    speaker,
                    speakerPlan.MessageCount,
                    roundNumber,
                    isClosing,
                    cancellationToken);

                if (batch.Contents.Count != speakerPlan.MessageCount)
                {
                    throw new InvalidOperationException(
                        $"计划生成 {speakerPlan.MessageCount} 条消息，实际返回 {batch.Contents.Count} 条。");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return RoundExecutionAttempt.Failed(
                    progressedSession,
                    AutonomousGroupChatSessionEndReason.GenerationFailed,
                    $"{speaker.Nickname} 的群消息生成失败：{exception.Message}",
                    messageTime);
            }

            List<GroupMessage> savedSpeakerMessages = new();
            for (int index = 0; index < batch.Contents.Count; index++)
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
                    await _replyTimingScheduler.WaitForConsecutiveMessageAsync(
                        speaker.Id,
                        messageTime,
                        cancellationToken);
                }

                messageTime = DateTime.Now;
                if (!_sessionService.TryAppendMessage(
                        round.Id,
                        speaker,
                        batch.Contents[index],
                        messageTime,
                        out GroupMessage? message,
                        out progressedSession,
                        out errorMessage)
                    || message is null)
                {
                    ApplyIdentityContinuity(
                        groupChat.Id,
                        batch,
                        savedSpeakerMessages);
                    return RoundExecutionAttempt.Failed(
                        progressedSession,
                        AutonomousGroupChatSessionEndReason
                            .MessagePersistenceFailed,
                        errorMessage,
                        messageTime);
                }

                messages.Add(message);
                savedSpeakerMessages.Add(message);
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
        int messageCount,
        int roundNumber,
        bool isClosing,
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
        GroupMessage? latestSessionMessage = recentHistory.LastOrDefault(
            message => message.AutonomousGroupChatSessionId == session.Id);
        AiDialogueReplyTarget replyTarget = CreateReplyTarget(
            latestSessionMessage,
            speaker,
            roundNumber,
            isClosing,
            plan.InitiatorAiAccountId);
        AiMessageGenerationRequest request = new()
        {
            Scenario = AiMessageGenerationScenario.AutonomousGroupChat,
            Speaker = speaker,
            OtherParticipants = participants
                .Where(account => account.Id != speaker.Id)
                .OrderByDescending(account =>
                    account.Id == latestSessionMessage?.SenderAiAccountId)
                .ToList()
                .AsReadOnly(),
            PrimarySpeaker = participants.Single(account =>
                account.Id == plan.InitiatorAiAccountId),
            Topic = plan.Topic,
            FocusContent = latestSessionMessage?.Content ?? plan.Topic,
            ReplyTarget = replyTarget,
            RecentMessages = recentMessages,
            ExpectedMessageCount = messageCount,
            RoundNumber = roundNumber,
            IsInitiator = speaker.Id == plan.InitiatorAiAccountId,
            OtherParticipantHasResponded = latestSessionMessage is not null,
            SpeakerToOtherRelationshipScore =
                plan.Decision.AverageRelationshipScore,
            OtherToSpeakerRelationshipScore =
                plan.Decision.AverageRelationshipScore,
            QuestionPolicy = _questionPolicyService.CreatePolicy(
                speaker.Id,
                recentMessages)
        };
        request = _identityContinuityService.PrepareGenerationRequest(request);
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
            ActionPlan = directionPlan.ActionPlan
        };

        IReadOnlyList<string> contents =
            await _messageGenerator.GenerateMessagesAsync(
            request,
            cancellationToken);
        return new AiDirectedMessageBatch(request, continuityPlan, contents);
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

    private static AiDialogueReplyTarget CreateReplyTarget(
        GroupMessage? latestSessionMessage,
        AiAccount speaker,
        int roundNumber,
        bool isClosing,
        Guid initiatorId)
    {
        if (isClosing)
        {
            return AiDialogueReplyTarget.CloseConversation(
                latestSessionMessage is null
                    ? null
                    : ToDialogueMessage(latestSessionMessage));
        }

        if (roundNumber == 1 && speaker.Id == initiatorId
            && latestSessionMessage is null)
        {
            return AiDialogueReplyTarget.OpenTopic();
        }

        if (latestSessionMessage?.SenderAiAccountId == speaker.Id)
        {
            return AiDialogueReplyTarget.ContinueTopic();
        }

        return latestSessionMessage is null
            ? AiDialogueReplyTarget.ContinueTopic()
            : AiDialogueReplyTarget.ReplyTo(
                ToDialogueMessage(latestSessionMessage));
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

using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 协调一次受控的自主好友群聊：判断、规划、建群、选择多位发言者并保存正式消息。
/// </summary>
public sealed class AutonomousGroupChatExecutionService
{
    private readonly AutonomousGroupChatJudge _judge;
    private readonly AutonomousGroupChatPlanningService _planningService;
    private readonly AutonomousGroupChatSpeakerPlanner _speakerPlanner;
    private readonly AutonomousGroupChatSessionService _sessionService;
    private readonly AiAccountService _aiAccountService;
    private readonly GroupChatService _groupChatService;
    private readonly GroupMessageService _groupMessageService;
    private readonly IConversationDirector _conversationDirector;
    private readonly IAiMessageGenerator _messageGenerator;

    public AutonomousGroupChatExecutionService(
        AutonomousGroupChatJudge judge,
        AutonomousGroupChatPlanningService planningService,
        AutonomousGroupChatSpeakerPlanner speakerPlanner,
        AutonomousGroupChatSessionService sessionService,
        AiAccountService aiAccountService,
        GroupChatService groupChatService,
        GroupMessageService groupMessageService,
        IConversationDirector conversationDirector,
        IAiMessageGenerator messageGenerator)
    {
        _judge = judge;
        _planningService = planningService;
        _speakerPlanner = speakerPlanner;
        _sessionService = sessionService;
        _aiAccountService = aiAccountService;
        _groupChatService = groupChatService;
        _groupMessageService = groupMessageService;
        _conversationDirector = conversationDirector;
        _messageGenerator = messageGenerator;
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

        IReadOnlyList<Guid> speakerIds = _speakerPlanner.Plan(plan);
        List<GroupMessage> savedMessages = new();
        DateTime messageTime = evaluatedAt;

        foreach (Guid speakerId in speakerIds)
        {
            AiAccount speaker = participants.Single(account => account.Id == speakerId);
            GroupMessage? latestSessionMessage = savedMessages.LastOrDefault();
            IReadOnlyList<string> contents;

            try
            {
                contents = await GenerateMessagesAsync(
                    groupChat,
                    plan,
                    participants,
                    speaker,
                    latestSessionMessage,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return FailSession(
                    AutonomousGroupChatExecutionStatus.GenerationFailed,
                    AutonomousGroupChatSessionEndReason.GenerationFailed,
                    decision,
                    groupChat,
                    groupChatCreated,
                    session,
                    savedMessages,
                    messageTime,
                    $"{speaker.Nickname} 的群消息生成失败：{exception.Message}");
            }

            foreach (string content in contents)
            {
                messageTime = messageTime.AddTicks(1);
                if (!_sessionService.TryAppendMessage(
                        session.Id,
                        speaker,
                        content,
                        messageTime,
                        out GroupMessage? message,
                        out AutonomousGroupChatSession? progressedSession,
                        out string messageError)
                    || message is null)
                {
                    return FailSession(
                        AutonomousGroupChatExecutionStatus.MessagePersistenceFailed,
                        AutonomousGroupChatSessionEndReason.MessagePersistenceFailed,
                        decision,
                        groupChat,
                        groupChatCreated,
                        progressedSession ?? session,
                        savedMessages,
                        messageTime,
                        messageError);
                }

                session = progressedSession ?? session;
                savedMessages.Add(message);
            }
        }

        if (!_sessionService.TryCompleteSession(
                session.Id,
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
                savedMessages,
                $"群消息已经保存，但 Session 状态更新失败。{completionError}");
        }

        return CreateResult(
            AutonomousGroupChatExecutionStatus.Completed,
            decision,
            groupChat,
            groupChatCreated,
            completedSession,
            savedMessages);
    }

    private async Task<IReadOnlyList<string>> GenerateMessagesAsync(
        GroupChat groupChat,
        AutonomousGroupChatPlan plan,
        IReadOnlyList<AiAccount> participants,
        AiAccount speaker,
        GroupMessage? latestSessionMessage,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AiDialogueMessage> recentMessages = _groupMessageService
            .GetOrderedChatHistory(groupChat)
            .TakeLast(12)
            .Select(ToDialogueMessage)
            .ToList()
            .AsReadOnly();
        bool isInitiator = speaker.Id == plan.InitiatorAiAccountId;
        AiDialogueReplyTarget replyTarget = latestSessionMessage is null
            ? AiDialogueReplyTarget.OpenTopic()
            : AiDialogueReplyTarget.ReplyTo(
                ToDialogueMessage(latestSessionMessage));
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
            AllowedMessageCountRange = new AiMessageCountRange(1, 3),
            ExpectedMessageCount = 1,
            RoundNumber = 1,
            IsInitiator = isInitiator
        };
        ConversationDirectionPlan directionPlan =
            await _conversationDirector.CreatePlanAsync(
                request,
                cancellationToken);
        request = request with
        {
            ExpectedMessageCount = directionPlan.SelectedMessageCount,
            DirectionPlan = directionPlan,
            ActionPlan = directionPlan.ActionPlan
        };

        return await _messageGenerator.GenerateMessagesAsync(
            request,
            cancellationToken);
    }

    private AutonomousGroupChatExecutionResult FailSession(
        AutonomousGroupChatExecutionStatus status,
        AutonomousGroupChatSessionEndReason endReason,
        AutonomousGroupChatDecision decision,
        GroupChat groupChat,
        bool groupChatCreated,
        AutonomousGroupChatSession session,
        IReadOnlyList<GroupMessage> savedMessages,
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
            savedMessages,
            errorMessage);
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
            Messages = messages ?? Array.Empty<GroupMessage>(),
            ErrorMessage = errorMessage
        };
    }
}

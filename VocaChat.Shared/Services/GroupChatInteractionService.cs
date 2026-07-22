using System;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 协调保存用户消息、选择 AI、生成模拟回复和保存 AI 消息的共享流程。
/// </summary>
public sealed class GroupChatInteractionService
{
    private readonly GroupMessageService _groupMessageService;
    private readonly IAiMessageGenerator _messageGenerator;
    private readonly GroupChatReplyPlanner _replyPlanner;
    private readonly IGroupConversationDirector _groupConversationDirector;
    private readonly GroupConversationPlanValidator _groupPlanValidator;
    private readonly IConversationDirector _conversationDirector;
    private readonly AiReplyTimingScheduler _replyTimingScheduler;
    private readonly AiReplyMessageCountSettingsResolver
        _replyMessageCountSettingsResolver;
    private readonly ConversationQuestionPolicyService _questionPolicyService;
    private readonly AiIdentityContinuityService _identityContinuityService;
    private readonly GroupConversationContextService _conversationContextService;
    private readonly GroupConversationDensitySettingsResolver _densityResolver;
    private readonly GroupConversationDiagnosticService _groupDiagnosticService;

    public GroupChatInteractionService(
        GroupMessageService groupMessageService,
        IAiMessageGenerator messageGenerator,
        GroupChatReplyPlanner replyPlanner,
        IGroupConversationDirector groupConversationDirector,
        GroupConversationPlanValidator groupPlanValidator,
        IConversationDirector conversationDirector,
        AiReplyTimingScheduler replyTimingScheduler,
        AiReplyMessageCountSettingsResolver replyMessageCountSettingsResolver,
        ConversationQuestionPolicyService questionPolicyService,
        AiIdentityContinuityService identityContinuityService,
        GroupConversationContextService conversationContextService,
        GroupConversationDensitySettingsResolver densityResolver,
        GroupConversationDiagnosticService groupDiagnosticService)
    {
        _groupMessageService = groupMessageService
            ?? throw new ArgumentNullException(nameof(groupMessageService));
        _messageGenerator = messageGenerator
            ?? throw new ArgumentNullException(nameof(messageGenerator));
        _replyPlanner = replyPlanner
            ?? throw new ArgumentNullException(nameof(replyPlanner));
        _groupConversationDirector = groupConversationDirector
            ?? throw new ArgumentNullException(
                nameof(groupConversationDirector));
        _groupPlanValidator = groupPlanValidator
            ?? throw new ArgumentNullException(nameof(groupPlanValidator));
        _conversationDirector = conversationDirector
            ?? throw new ArgumentNullException(nameof(conversationDirector));
        _replyTimingScheduler = replyTimingScheduler
            ?? throw new ArgumentNullException(nameof(replyTimingScheduler));
        _replyMessageCountSettingsResolver = replyMessageCountSettingsResolver
            ?? throw new ArgumentNullException(
                nameof(replyMessageCountSettingsResolver));
        _questionPolicyService = questionPolicyService
            ?? throw new ArgumentNullException(nameof(questionPolicyService));
        _identityContinuityService = identityContinuityService
            ?? throw new ArgumentNullException(nameof(identityContinuityService));
        _conversationContextService = conversationContextService
            ?? throw new ArgumentNullException(
                nameof(conversationContextService));
        _densityResolver = densityResolver
            ?? throw new ArgumentNullException(nameof(densityResolver));
        _groupDiagnosticService = groupDiagnosticService
            ?? throw new ArgumentNullException(nameof(groupDiagnosticService));
    }

    /// <summary>
    /// 执行一轮群聊交互；用户消息保存后，即使 AI 回复失败也不会回滚。
    /// </summary>
    public async Task<GroupChatInteractionResult> ProcessUserMessageAsync(
        GroupChat groupChat,
        string content,
        CancellationToken cancellationToken = default)
    {
        return await ProcessUserMessageAsync(
            groupChat,
            content,
            null,
            cancellationToken);
    }

    /// <summary>
    /// 使用客户端预先生成的消息 Id 执行群聊交互，便于界面立即展示待发送消息。
    /// </summary>
    public async Task<GroupChatInteractionResult> ProcessUserMessageAsync(
        GroupChat groupChat,
        string content,
        Guid? userMessageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupChat);

        Guid interactionBatchId = Guid.NewGuid();
        bool userMessageSaved =
            _groupMessageService.TrySaveUserInteractionMessage(
            groupChat,
            content,
            userMessageId,
            interactionBatchId,
            out GroupMessage? userMessage,
            out string userMessageError);

        if (!userMessageSaved || userMessage is null)
        {
            return GroupChatInteractionResult.UserMessageRejected(
                userMessageError);
        }

        if (groupChat.Members.Count == 0)
        {
            return GroupChatInteractionResult.AiReplyFailed(
                userMessage,
                Array.Empty<GroupMessage>(),
                AiSpeakerSelectionStatus.NotAttempted,
                "当前群聊没有 AI 成员，无法生成假回复。");
        }

        IReadOnlyList<GroupMessage> planningHistory = _groupMessageService
            .GetOrderedChatHistory(groupChat)
            .TakeLast(12)
            .ToList()
            .AsReadOnly();
        GroupConversationDensitySettings densitySettings =
            _densityResolver.Resolve();
        GroupConversationPlanningRequest planningRequest = new()
        {
            GroupChat = groupChat,
            AnchorMessage = userMessage,
            RecentMessages = planningHistory,
            MaximumSpeakerCount = Math.Min(
                groupChat.Members.Count,
                densitySettings.ResolveMaximumSpeakerCount(
                    groupChat,
                    userMessage.Content)),
            MaximumTotalMessageCount =
                densitySettings.MaximumMessagesPerTurn
        };
        GroupConversationTurnPlan turnPlan;
        try
        {
            turnPlan = await CreateValidatedPlanAsync(
                planningRequest,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return GroupChatInteractionResult.AiReplyFailed(
                userMessage,
                Array.Empty<GroupMessage>(),
                AiSpeakerSelectionStatus.NotAttempted,
                exception.Message);
        }

        if (turnPlan.Speakers.Count == 0)
        {
            return GroupChatInteractionResult.AiReplyFailed(
                userMessage,
                Array.Empty<GroupMessage>(),
                turnPlan.SelectionStatus,
                "群聊导演没有选择可执行的 AI 发言者。");
        }

        List<GroupMessage> savedAiReplies = new();
        AiAccount primarySpeaker = groupChat.Members.Single(member =>
            member.Id == turnPlan.Speakers[0].SpeakerAiAccountId);
        AiDialogueMessage conversationAnchor = new(
            userMessage.SenderDisplayName,
            userMessage.Content,
            userMessage.SenderType,
            userMessage.SenderAiAccountId,
            userMessage.Id,
            userMessage.SentAt);

        for (int speakerIndex = 0;
             speakerIndex < turnPlan.Speakers.Count;
             speakerIndex++)
        {
            GroupConversationSpeakerPlan speakerPlan =
                turnPlan.Speakers[speakerIndex];
            AiAccount speaker = groupChat.Members.Single(member =>
                member.Id == speakerPlan.SpeakerAiAccountId);
            IReadOnlyList<string> replyContents;
            Guid replyToMessageId = userMessage.Id;
            AiMessageGenerationRequest? completedRequest = null;
            AiIdentityContinuityPlan? continuityPlan = null;

            try
            {
                IReadOnlyList<GroupMessage> recentHistory =
                    _groupMessageService.GetOrderedChatHistory(groupChat)
                        .TakeLast(12)
                        .ToList();
                GroupMessage targetMessage = ResolveTargetMessage(
                    speakerPlan,
                    recentHistory,
                    savedAiReplies,
                    userMessage);
                replyToMessageId = targetMessage.Id;
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
                AiDialogueMessage replyTarget = new(
                    targetMessage.SenderDisplayName,
                    targetMessage.Content,
                    targetMessage.SenderType,
                    targetMessage.SenderAiAccountId,
                    targetMessage.Id,
                    targetMessage.SentAt);
                AiAccount? relationshipTarget = ResolveRelationshipTarget(
                    groupChat,
                    speakerPlan,
                    targetMessage);
                AiMessageGenerationRequest generationRequest = new()
                {
                    Scenario = speakerPlan.SpeakerAiAccountId ==
                            primarySpeaker.Id
                        ? AiMessageGenerationScenario.GroupPrimaryReply
                        : AiMessageGenerationScenario.GroupFollowUpReply,
                    Speaker = speaker,
                    OtherParticipants = groupChat.Members
                        .Where(member => member.Id != speaker.Id)
                        .ToList()
                        .AsReadOnly(),
                    PrimarySpeaker = primarySpeaker,
                    Topic = turnPlan.TopicFocus,
                    FocusContent = replyTarget.Content,
                    ReplyTarget = AiDialogueReplyTarget.ReplyTo(replyTarget),
                    ConversationAnchor = conversationAnchor,
                    RecentMessages = recentMessages,
                    QuestionPolicy = _questionPolicyService.CreatePolicy(
                        speaker.Id,
                        recentMessages),
                    AllowedMessageCountRange = ApplyMessageBudget(
                        _replyMessageCountSettingsResolver.Resolve(speaker.Id),
                        planningRequest.MaximumTotalMessageCount,
                        savedAiReplies.Count,
                        turnPlan.Speakers.Count - speakerIndex - 1),
                    ExpectedMessageCount = 1,
                    GroupConversationPlan = speakerPlan
                };
                generationRequest = _conversationContextService
                    .PrepareGenerationRequest(
                        generationRequest,
                        relationshipTarget);
                ConversationDirectionPlan directionPlan =
                    await _conversationDirector.CreatePlanAsync(
                        generationRequest,
                        cancellationToken);
                continuityPlan = _identityContinuityService
                    .ValidateDirectionPlan(generationRequest, directionPlan);
                directionPlan = continuityPlan.DirectionPlan;
                generationRequest = generationRequest with
                {
                    DirectionPlan = directionPlan,
                    ActionPlan = directionPlan.ActionPlan,
                    ExpectedMessageCount = directionPlan.SelectedMessageCount
                };
                replyContents =
                    await _messageGenerator.GenerateMessagesAsync(
                        generationRequest,
                        cancellationToken);
                completedRequest = generationRequest;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                IReadOnlyList<GroupMessage> savedReplies =
                    savedAiReplies.AsReadOnly();
                _groupDiagnosticService.RecordFailure(
                    speakerPlan.SpeakerAiAccountId == primarySpeaker.Id
                        ? AiMessageGenerationScenario.GroupPrimaryReply
                        : AiMessageGenerationScenario.GroupFollowUpReply,
                    groupChat.Id,
                    speaker.Id,
                    "消息生成",
                    exception.Message,
                    savedAiReplies.Count > 0);
                return savedAiReplies.Count == 0
                    ? GroupChatInteractionResult.AiReplyFailed(
                        userMessage,
                        savedReplies,
                        turnPlan.SelectionStatus,
                        exception.Message)
                    : GroupChatInteractionResult.PartiallySucceeded(
                        userMessage,
                        savedReplies,
                        turnPlan.SelectionStatus,
                        exception.Message);
            }

            if (savedAiReplies.Count + replyContents.Count
                > planningRequest.MaximumTotalMessageCount)
            {
                IReadOnlyList<GroupMessage> savedReplies =
                    savedAiReplies.AsReadOnly();
                const string errorMessage =
                    "群聊回复超过本轮允许的 AI 消息总数。";
                _groupDiagnosticService.RecordFailure(
                    AiMessageGenerationScenario.GroupFollowUpReply,
                    groupChat.Id,
                    speaker.Id,
                    "消息数量控制",
                    errorMessage,
                    savedAiReplies.Count > 0);
                return savedAiReplies.Count == 0
                    ? GroupChatInteractionResult.AiReplyFailed(
                        userMessage,
                        savedReplies,
                        turnPlan.SelectionStatus,
                        errorMessage)
                    : GroupChatInteractionResult.PartiallySucceeded(
                        userMessage,
                        savedReplies,
                        turnPlan.SelectionStatus,
                        errorMessage);
            }

            List<GroupMessage> candidateReplies = new();
            for (int replyIndex = 0;
                 replyIndex < replyContents.Count;
                 replyIndex++)
            {
                GroupMessage previousMessage =
                    savedAiReplies.LastOrDefault() ?? userMessage;
                try
                {
                    if (replyIndex == 0)
                    {
                        await _replyTimingScheduler.WaitForReplyAsync(
                            speaker.Id,
                            previousMessage.SentAt,
                            cancellationToken);
                    }
                    else
                    {
                        await _replyTimingScheduler
                            .WaitForConsecutiveMessageAsync(
                                speaker.Id,
                                previousMessage.SentAt,
                                cancellationToken);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    ApplyIdentityContinuity(
                        groupChat.Id,
                        completedRequest,
                        continuityPlan,
                        candidateReplies);
                    IReadOnlyList<GroupMessage> savedReplies =
                        savedAiReplies.AsReadOnly();
                    _groupDiagnosticService.RecordFailure(
                        speakerPlan.SpeakerAiAccountId == primarySpeaker.Id
                            ? AiMessageGenerationScenario.GroupPrimaryReply
                            : AiMessageGenerationScenario.GroupFollowUpReply,
                        groupChat.Id,
                        speaker.Id,
                        "回复等待",
                        exception.Message,
                        savedAiReplies.Count > 0);
                    return savedAiReplies.Count == 0
                        ? GroupChatInteractionResult.AiReplyFailed(
                            userMessage,
                            savedReplies,
                            turnPlan.SelectionStatus,
                            exception.Message)
                        : GroupChatInteractionResult.PartiallySucceeded(
                            userMessage,
                            savedReplies,
                            turnPlan.SelectionStatus,
                            exception.Message);
                }

                bool aiMessageSaved =
                    _groupMessageService.TrySaveAiInteractionReply(
                    groupChat,
                    speaker,
                    replyContents[replyIndex],
                    interactionBatchId,
                    replyToMessageId,
                    out GroupMessage? aiMessage,
                    out string aiMessageError);

                if (!aiMessageSaved || aiMessage is null)
                {
                    ApplyIdentityContinuity(
                        groupChat.Id,
                        completedRequest,
                        continuityPlan,
                        candidateReplies);
                    IReadOnlyList<GroupMessage> savedReplies =
                        savedAiReplies.AsReadOnly();
                    _groupDiagnosticService.RecordFailure(
                        speakerPlan.SpeakerAiAccountId == primarySpeaker.Id
                            ? AiMessageGenerationScenario.GroupPrimaryReply
                            : AiMessageGenerationScenario.GroupFollowUpReply,
                        groupChat.Id,
                        speaker.Id,
                        "消息保存",
                        aiMessageError,
                        savedAiReplies.Count > 0);
                    return savedAiReplies.Count == 0
                        ? GroupChatInteractionResult.AiReplyFailed(
                            userMessage,
                            savedReplies,
                            turnPlan.SelectionStatus,
                            aiMessageError)
                        : GroupChatInteractionResult.PartiallySucceeded(
                            userMessage,
                            savedReplies,
                            turnPlan.SelectionStatus,
                            aiMessageError);
                }

                savedAiReplies.Add(aiMessage);
                candidateReplies.Add(aiMessage);
            }

            if (completedRequest is not null && continuityPlan is not null)
            {
                ApplyIdentityContinuity(
                    groupChat.Id,
                    completedRequest,
                    continuityPlan,
                    candidateReplies);
            }
        }

        return GroupChatInteractionResult.Succeeded(
            userMessage,
            savedAiReplies.AsReadOnly(),
            turnPlan.SelectionStatus);
    }

    /// <summary>
    /// 执行群级导演并在模型计划越界时使用现有规则重新建立安全计划。
    /// </summary>
    private async Task<GroupConversationTurnPlan> CreateValidatedPlanAsync(
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
            // 第三方模型或自定义导演失败时，继续使用受业务规则保护的回退计划。
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
                AiMessageGenerationScenario.GroupPrimaryReply,
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
                AiMessageGenerationScenario.GroupPrimaryReply,
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

    /// <summary>
    /// 在总量上限内为当前发言者保留消息空间，并给后续发言者各留一条。
    /// </summary>
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
                "群聊单轮消息总量不足以让已选中的好友完成发言。");
        }

        return new AiMessageCountRange(Math.Max(1, minimum), maximum);
    }

    /// <summary>
    /// 将群级计划中的逻辑受众解析成当前已保存的实际目标消息。
    /// </summary>
    private static GroupMessage ResolveTargetMessage(
        GroupConversationSpeakerPlan speakerPlan,
        IReadOnlyList<GroupMessage> recentHistory,
        IReadOnlyList<GroupMessage> savedAiReplies,
        GroupMessage anchorMessage)
    {
        if (speakerPlan.Audience ==
                GroupConversationAudience.SpecificAiAccount
            && speakerPlan.TargetAiAccountId is Guid targetAiAccountId)
        {
            GroupMessage? targetAiMessage = savedAiReplies
                .LastOrDefault(message =>
                    message.SenderAiAccountId == targetAiAccountId)
                ?? recentHistory.LastOrDefault(message =>
                    message.SenderAiAccountId == targetAiAccountId);

            if (targetAiMessage is not null)
            {
                return targetAiMessage;
            }
        }

        return savedAiReplies
                .Concat(recentHistory)
                .LastOrDefault(message =>
                    message.Id == speakerPlan.ReplyTargetMessageId)
            ?? anchorMessage;
    }

    /// <summary>
    /// 只在计划或实际目标消息明确指向群内 AI 时建立关系上下文；
    /// 回应本地用户和泛指全群时不会任意挑选一个成员。
    /// </summary>
    private static AiAccount? ResolveRelationshipTarget(
        GroupChat groupChat,
        GroupConversationSpeakerPlan speakerPlan,
        GroupMessage targetMessage)
    {
        Guid? targetAiAccountId = targetMessage.SenderAiAccountId;
        if (targetAiAccountId is null
            && speakerPlan.Audience ==
                GroupConversationAudience.SpecificAiAccount)
        {
            targetAiAccountId = speakerPlan.TargetAiAccountId;
        }

        return targetAiAccountId is null
            ? null
            : groupChat.Members.SingleOrDefault(member =>
                member.Id == targetAiAccountId
                && member.Id != speakerPlan.SpeakerAiAccountId);
    }

    private void ApplyIdentityContinuity(
        Guid groupChatId,
        AiMessageGenerationRequest? request,
        AiIdentityContinuityPlan? continuityPlan,
        IReadOnlyList<GroupMessage> messages)
    {
        if (request is null || continuityPlan is null || messages.Count == 0)
        {
            return;
        }

        _identityContinuityService.ApplyAfterMessagesSaved(
            request,
            continuityPlan,
            groupChatId,
            messages.Select(message => new AiPersistedMessageEvidence(
                    message.Id,
                    message.Content,
                    message.SentAt))
                .ToList()
                .AsReadOnly());
    }
}

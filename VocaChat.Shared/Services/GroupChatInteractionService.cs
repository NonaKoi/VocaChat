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
    private readonly IConversationDirector _conversationDirector;
    private readonly AiReplyTimingScheduler _replyTimingScheduler;
    private readonly ConversationQuestionPolicyService _questionPolicyService;
    private readonly AiIdentityContinuityService _identityContinuityService;

    public GroupChatInteractionService(
        GroupMessageService groupMessageService,
        IAiMessageGenerator messageGenerator,
        GroupChatReplyPlanner replyPlanner,
        IConversationDirector conversationDirector,
        AiReplyTimingScheduler replyTimingScheduler,
        ConversationQuestionPolicyService questionPolicyService,
        AiIdentityContinuityService identityContinuityService)
    {
        _groupMessageService = groupMessageService
            ?? throw new ArgumentNullException(nameof(groupMessageService));
        _messageGenerator = messageGenerator
            ?? throw new ArgumentNullException(nameof(messageGenerator));
        _replyPlanner = replyPlanner
            ?? throw new ArgumentNullException(nameof(replyPlanner));
        _conversationDirector = conversationDirector
            ?? throw new ArgumentNullException(nameof(conversationDirector));
        _replyTimingScheduler = replyTimingScheduler
            ?? throw new ArgumentNullException(nameof(replyTimingScheduler));
        _questionPolicyService = questionPolicyService
            ?? throw new ArgumentNullException(nameof(questionPolicyService));
        _identityContinuityService = identityContinuityService
            ?? throw new ArgumentNullException(nameof(identityContinuityService));
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

        bool userMessageSaved = _groupMessageService.TrySaveUserMessage(
            groupChat,
            content,
            userMessageId,
            out GroupMessage? userMessage,
            out string userMessageError);

        if (!userMessageSaved || userMessage is null)
        {
            return GroupChatInteractionResult.UserMessageRejected(
                userMessageError);
        }

        GroupChatReplyPlan replyPlan = _replyPlanner.CreatePlan(
            groupChat,
            userMessage.Content);

        if (replyPlan.Candidates.Count == 0)
        {
            return GroupChatInteractionResult.AiReplyFailed(
                userMessage,
                Array.Empty<GroupMessage>(),
                AiSpeakerSelectionStatus.NotAttempted,
                "当前群聊没有 AI 成员，无法生成假回复。");
        }

        List<GroupMessage> savedAiReplies = new();
        AiAccount primarySpeaker = replyPlan.Candidates[0].Speaker;
        AiDialogueMessage conversationAnchor = new(
            userMessage.SenderDisplayName,
            userMessage.Content,
            userMessage.SenderType,
            userMessage.SenderAiAccountId,
            userMessage.Id,
            userMessage.SentAt);

        foreach (GroupChatReplyCandidate candidate in replyPlan.Candidates)
        {
            string replyContent;
            AiMessageGenerationRequest? completedRequest = null;
            AiIdentityContinuityPlan? continuityPlan = null;

            try
            {
                IReadOnlyList<GroupMessage> recentHistory =
                    _groupMessageService.GetOrderedChatHistory(groupChat)
                        .TakeLast(12)
                        .ToList();
                GroupMessage targetMessage = candidate.Role ==
                        GroupChatReplyRole.FollowUp
                    ? savedAiReplies.LastOrDefault() ?? userMessage
                    : userMessage;
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
                AiMessageGenerationRequest generationRequest = new()
                {
                    Scenario = candidate.Role == GroupChatReplyRole.Primary
                        ? AiMessageGenerationScenario.GroupPrimaryReply
                        : AiMessageGenerationScenario.GroupFollowUpReply,
                    Speaker = candidate.Speaker,
                    OtherParticipants = groupChat.Members
                        .Where(member => member.Id != candidate.Speaker.Id)
                        .ToList()
                        .AsReadOnly(),
                    PrimarySpeaker = primarySpeaker,
                    FocusContent = replyTarget.Content,
                    ReplyTarget = AiDialogueReplyTarget.ReplyTo(replyTarget),
                    ConversationAnchor = conversationAnchor,
                    RecentMessages = recentMessages,
                    QuestionPolicy = _questionPolicyService.CreatePolicy(
                        candidate.Speaker.Id,
                        recentMessages),
                    ExpectedMessageCount = 1
                };
                generationRequest = _identityContinuityService
                    .PrepareGenerationRequest(generationRequest);
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
                    ActionPlan = directionPlan.ActionPlan
                };
                IReadOnlyList<string> generatedMessages =
                    await _messageGenerator.GenerateMessagesAsync(
                        generationRequest,
                        cancellationToken);
                replyContent = generatedMessages.Single();
                completedRequest = generationRequest;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                IReadOnlyList<GroupMessage> savedReplies =
                    savedAiReplies.AsReadOnly();
                return savedAiReplies.Count == 0
                    ? GroupChatInteractionResult.AiReplyFailed(
                        userMessage,
                        savedReplies,
                        replyPlan.SelectionStatus,
                        exception.Message)
                    : GroupChatInteractionResult.PartiallySucceeded(
                        userMessage,
                        savedReplies,
                        replyPlan.SelectionStatus,
                        exception.Message);
            }

            GroupMessage previousMessage =
                savedAiReplies.LastOrDefault() ?? userMessage;
            try
            {
                await _replyTimingScheduler.WaitForReplyAsync(
                    candidate.Speaker.Id,
                    previousMessage.SentAt,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                IReadOnlyList<GroupMessage> savedReplies =
                    savedAiReplies.AsReadOnly();
                return savedAiReplies.Count == 0
                    ? GroupChatInteractionResult.AiReplyFailed(
                        userMessage,
                        savedReplies,
                        replyPlan.SelectionStatus,
                        exception.Message)
                    : GroupChatInteractionResult.PartiallySucceeded(
                        userMessage,
                        savedReplies,
                        replyPlan.SelectionStatus,
                        exception.Message);
            }

            bool aiMessageSaved = _groupMessageService.TrySaveAiReply(
                groupChat,
                candidate.Speaker,
                replyContent,
                out GroupMessage? aiMessage,
                out string aiMessageError);

            if (!aiMessageSaved || aiMessage is null)
            {
                IReadOnlyList<GroupMessage> savedReplies =
                    savedAiReplies.AsReadOnly();
                return savedAiReplies.Count == 0
                    ? GroupChatInteractionResult.AiReplyFailed(
                        userMessage,
                        savedReplies,
                        replyPlan.SelectionStatus,
                        aiMessageError)
                    : GroupChatInteractionResult.PartiallySucceeded(
                        userMessage,
                        savedReplies,
                        replyPlan.SelectionStatus,
                        aiMessageError);
            }

            savedAiReplies.Add(aiMessage);
            if (completedRequest is not null && continuityPlan is not null)
            {
                _identityContinuityService.ApplyAfterMessagesSaved(
                    completedRequest,
                    continuityPlan,
                    groupChat.Id,
                    new[]
                    {
                        new AiPersistedMessageEvidence(
                            aiMessage.Id,
                            aiMessage.Content,
                            aiMessage.SentAt)
                    });
            }
        }

        return GroupChatInteractionResult.Succeeded(
            userMessage,
            savedAiReplies.AsReadOnly(),
            replyPlan.SelectionStatus);
    }
}

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

    public GroupChatInteractionService(
        GroupMessageService groupMessageService,
        IAiMessageGenerator messageGenerator,
        GroupChatReplyPlanner replyPlanner,
        IConversationDirector conversationDirector)
    {
        _groupMessageService = groupMessageService
            ?? throw new ArgumentNullException(nameof(groupMessageService));
        _messageGenerator = messageGenerator
            ?? throw new ArgumentNullException(nameof(messageGenerator));
        _replyPlanner = replyPlanner
            ?? throw new ArgumentNullException(nameof(replyPlanner));
        _conversationDirector = conversationDirector
            ?? throw new ArgumentNullException(nameof(conversationDirector));
    }

    /// <summary>
    /// 执行一轮群聊交互；用户消息保存后，即使 AI 回复失败也不会回滚。
    /// </summary>
    public async Task<GroupChatInteractionResult> ProcessUserMessageAsync(
        GroupChat groupChat,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupChat);

        bool userMessageSaved = _groupMessageService.TrySaveUserMessage(
            groupChat,
            content,
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
                    RecentMessages = recentHistory
                        .Select(message => new AiDialogueMessage(
                            message.SenderDisplayName,
                            message.Content,
                            message.SenderType,
                            message.SenderAiAccountId,
                            message.Id,
                            message.SentAt))
                        .ToList()
                        .AsReadOnly(),
                    ExpectedMessageCount = 1
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
                IReadOnlyList<string> generatedMessages =
                    await _messageGenerator.GenerateMessagesAsync(
                        generationRequest,
                        cancellationToken);
                replyContent = generatedMessages.Single();
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
        }

        return GroupChatInteractionResult.Succeeded(
            userMessage,
            savedAiReplies.AsReadOnly(),
            replyPlan.SelectionStatus);
    }
}

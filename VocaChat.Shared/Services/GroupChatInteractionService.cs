using System;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 协调保存用户消息、选择 AI、生成模拟回复和保存 AI 消息的共享流程。
/// </summary>
public sealed class GroupChatInteractionService
{
    private readonly GroupMessageService _groupMessageService;
    private readonly FakeAiReplyService _fakeAiReplyService;
    private readonly GroupChatReplyPlanner _replyPlanner;

    public GroupChatInteractionService(
        GroupMessageService groupMessageService,
        FakeAiReplyService fakeAiReplyService,
        GroupChatReplyPlanner replyPlanner)
    {
        _groupMessageService = groupMessageService
            ?? throw new ArgumentNullException(nameof(groupMessageService));
        _fakeAiReplyService = fakeAiReplyService
            ?? throw new ArgumentNullException(nameof(fakeAiReplyService));
        _replyPlanner = replyPlanner
            ?? throw new ArgumentNullException(nameof(replyPlanner));
    }

    /// <summary>
    /// 执行一轮群聊交互；用户消息保存后，即使 AI 回复失败也不会回滚。
    /// </summary>
    public GroupChatInteractionResult ProcessUserMessage(
        GroupChat groupChat,
        string content)
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

        foreach (GroupChatReplyCandidate candidate in replyPlan.Candidates)
        {
            string fakeReply = candidate.Role == GroupChatReplyRole.Primary
                ? _fakeAiReplyService.GenerateReply(
                    candidate.Speaker,
                    userMessage.Content)
                : _fakeAiReplyService.GenerateFollowUpReply(
                    candidate.Speaker,
                    primarySpeaker,
                    userMessage.Content);
            bool aiMessageSaved = _groupMessageService.TrySaveAiReply(
                groupChat,
                candidate.Speaker,
                fakeReply,
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

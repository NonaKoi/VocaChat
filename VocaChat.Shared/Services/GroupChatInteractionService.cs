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

    public GroupChatInteractionService(
        GroupMessageService groupMessageService,
        FakeAiReplyService fakeAiReplyService)
    {
        _groupMessageService = groupMessageService
            ?? throw new ArgumentNullException(nameof(groupMessageService));
        _fakeAiReplyService = fakeAiReplyService
            ?? throw new ArgumentNullException(nameof(fakeAiReplyService));
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

        AiAccount? aiSpeaker = _fakeAiReplyService.SelectAiSpeaker(
            groupChat,
            userMessage.Content,
            out bool selectedByMention);

        if (aiSpeaker is null)
        {
            return GroupChatInteractionResult.AiReplyFailed(
                userMessage,
                AiSpeakerSelectionStatus.NotAttempted,
                "当前群聊没有 AI 成员，无法生成假回复。");
        }

        AiSpeakerSelectionStatus selectionStatus = GetSelectionStatus(
            userMessage.Content,
            selectedByMention);
        string fakeReply = _fakeAiReplyService.GenerateReply(
            aiSpeaker,
            userMessage.Content);
        bool aiMessageSaved = _groupMessageService.TrySaveAiReply(
            groupChat,
            aiSpeaker,
            fakeReply,
            out GroupMessage? aiMessage,
            out string aiMessageError);

        if (!aiMessageSaved || aiMessage is null)
        {
            return GroupChatInteractionResult.AiReplyFailed(
                userMessage,
                selectionStatus,
                aiMessageError);
        }

        return GroupChatInteractionResult.Succeeded(
            userMessage,
            aiMessage,
            selectionStatus);
    }

    /// <summary>
    /// 将现有点名选择结果转换为 Console 和 Web API 都能理解的明确状态。
    /// </summary>
    private static AiSpeakerSelectionStatus GetSelectionStatus(
        string userContent,
        bool selectedByMention)
    {
        if (selectedByMention)
        {
            return AiSpeakerSelectionStatus.MentionMatched;
        }

        return userContent.Contains('@')
            ? AiSpeakerSelectionStatus.MentionNotMatched
            : AiSpeakerSelectionStatus.DefaultSelection;
    }
}

using System.Collections.Generic;
using System.Linq;
using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Services;

/// <summary>
/// 负责验证、创建和保存群消息，并返回按时间排序的聊天记录。
/// </summary>
public class GroupMessageService
{
    private readonly GroupChatService _groupChatService;

    /// <summary>
    /// 创建消息 Service，并使用群聊 Service 验证群聊和成员关系。
    /// </summary>
    public GroupMessageService(GroupChatService groupChatService)
    {
        _groupChatService = groupChatService;
    }

    /// <summary>
    /// 验证并保存本地用户消息；空白内容不会创建消息。
    /// </summary>
    public bool TrySaveUserMessage(
        GroupChat groupChat,
        string content,
        out GroupMessage? message,
        out string errorMessage)
    {
        message = null;

        if (_groupChatService.FindById(groupChat.Id) is null)
        {
            errorMessage = "群聊不存在，不能保存消息。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "消息内容不能为空。";
            return false;
        }

        GroupMessage userMessage = new(
            groupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            content.Trim());

        groupChat.AddMessage(userMessage);
        message = userMessage;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 验证并保存 AI 回复；未加入当前群聊的 AI 账号不能发送群消息。
    /// </summary>
    public bool TrySaveAiReply(
        GroupChat groupChat,
        AiAccount aiSpeaker,
        string content,
        out GroupMessage? message,
        out string errorMessage)
    {
        message = null;

        if (_groupChatService.FindById(groupChat.Id) is null)
        {
            errorMessage = "群聊不存在，不能保存消息。";
            return false;
        }

        if (!_groupChatService.IsMember(groupChat, aiSpeaker.Id))
        {
            errorMessage = "未加入当前群聊的 AI 账号不能发送群消息。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "AI 回复内容不能为空。";
            return false;
        }

        GroupMessage aiMessage = new(
            groupChat.Id,
            MessageSenderType.AiAccount,
            aiSpeaker.Nickname,
            aiSpeaker.Id,
            content.Trim());

        groupChat.AddMessage(aiMessage);
        message = aiMessage;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 返回当前群聊按发送时间从早到晚排列的只读消息列表。
    /// </summary>
    public IReadOnlyList<GroupMessage> GetOrderedChatHistory(GroupChat groupChat)
    {
        List<GroupMessage> orderedMessages = groupChat.Messages
            .OrderBy(message => message.SentAt)
            .ToList();

        return orderedMessages.AsReadOnly();
    }
}

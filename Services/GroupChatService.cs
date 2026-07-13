using System;
using System.Collections.Generic;
using System.Linq;
using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Services;

/// <summary>
/// 负责群聊创建、消息保存、AI 发言者选择和聊天记录排序。
/// </summary>
public class GroupChatService
{
    /// <summary>
    /// 验证群聊名称；验证失败时返回可显示的错误信息。
    /// </summary>
    public string? ValidateGroupChatName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? "群聊名称不能为空，请重新输入。"
            : null;
    }

    /// <summary>
    /// 使用用户已创建并选中的 AI 账号创建一个群聊。
    /// </summary>
    public GroupChat CreateGroupChat(string name, IEnumerable<AiAccount> selectedMembers)
    {
        string? validationError = ValidateGroupChatName(name);

        if (validationError is not null)
        {
            throw new ArgumentException(validationError, nameof(name));
        }

        List<AiAccount> members = new();

        foreach (AiAccount selectedMember in selectedMembers)
        {
            bool alreadyAdded = members.Any(member => member.Id == selectedMember.Id);

            if (!alreadyAdded)
            {
                members.Add(selectedMember);
            }
        }

        if (members.Count == 0)
        {
            throw new ArgumentException("群聊至少需要一个 AI 成员。", nameof(selectedMembers));
        }

        return new GroupChat(name.Trim(), members);
    }

    /// <summary>
    /// 保存一条本地用户消息；空白内容不会创建消息。
    /// </summary>
    public GroupMessage? SaveUserMessage(GroupChat groupChat, string content)
    {
        string trimmedContent = content.Trim();

        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            return null;
        }

        GroupMessage userMessage = new(
            groupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            trimmedContent);

        groupChat.Messages.Add(userMessage);
        return userMessage;
    }

    /// <summary>
    /// 只从当前群成员中选择一个 AI；有效点名优先，否则选择第一个成员。
    /// </summary>
    public AiAccount? SelectAiSpeaker(
        GroupChat groupChat,
        string userContent,
        out bool selectedByMention)
    {
        selectedByMention = false;

        if (groupChat.Members.Count == 0)
        {
            return null;
        }

        AiAccount? mentionedMember = null;
        int earliestMentionIndex = int.MaxValue;

        foreach (AiAccount member in groupChat.Members)
        {
            string mentionText = $"@{member.Nickname}";
            int mentionIndex = userContent.IndexOf(
                mentionText,
                StringComparison.OrdinalIgnoreCase);

            if (mentionIndex >= 0 && mentionIndex < earliestMentionIndex)
            {
                mentionedMember = member;
                earliestMentionIndex = mentionIndex;
            }
        }

        if (mentionedMember is not null)
        {
            selectedByMention = true;
            return mentionedMember;
        }

        return groupChat.Members[0];
    }

    /// <summary>
    /// 保存一条 AI 回复，并确保发言账号确实属于当前群聊。
    /// </summary>
    public GroupMessage SaveAiReply(
        GroupChat groupChat,
        AiAccount aiSpeaker,
        string content)
    {
        bool isGroupMember = groupChat.Members.Any(member => member.Id == aiSpeaker.Id);

        if (!isGroupMember)
        {
            throw new InvalidOperationException("未加入当前群聊的 AI 账号不能发送群消息。");
        }

        GroupMessage aiMessage = new(
            groupChat.Id,
            MessageSenderType.AiAccount,
            aiSpeaker.Nickname,
            aiSpeaker.Id,
            content.Trim());

        groupChat.Messages.Add(aiMessage);
        return aiMessage;
    }

    /// <summary>
    /// 返回当前群聊按发送时间从早到晚排列的全部消息。
    /// </summary>
    public IReadOnlyList<GroupMessage> GetOrderedChatHistory(GroupChat groupChat)
    {
        return groupChat.Messages
            .OrderBy(message => message.SentAt)
            .ToList();
    }
}

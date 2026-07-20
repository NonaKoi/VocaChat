using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责验证、创建和保存群消息，并返回按会话序号排序的聊天记录。
/// </summary>
public class GroupMessageService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    /// <summary>
    /// 创建消息 Service；每个保存或查询操作使用一个短生命周期 DbContext。
    /// </summary>
    public GroupMessageService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
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
        return TrySaveUserMessage(
            groupChat,
            content,
            null,
            out message,
            out errorMessage);
    }

    /// <summary>
    /// 使用客户端预先生成的消息 Id 保存用户消息，使前端乐观消息可以与落库结果对齐。
    /// </summary>
    public bool TrySaveUserMessage(
        GroupChat groupChat,
        string content,
        Guid? messageId,
        out GroupMessage? message,
        out string errorMessage)
    {
        message = null;

        if (messageId == Guid.Empty)
        {
            errorMessage = "消息标识无效。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "消息内容不能为空。";
            return false;
        }

        string trimmedContent = content.Trim();

        if (trimmedContent.Length > GroupMessage.ContentMaxLength)
        {
            errorMessage = $"消息内容不能超过 {GroupMessage.ContentMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        GroupChat? storedGroupChat = dbContext.GroupChats
            .AsNoTracking()
            .SingleOrDefault(storedGroupChat =>
                storedGroupChat.Id == groupChat.Id);

        if (storedGroupChat is null)
        {
            errorMessage = "群聊不存在，不能保存消息。";
            return false;
        }

        if (!storedGroupChat.IncludesLocalUser)
        {
            errorMessage = "你不在这个好友群聊中，不能发送用户消息。";
            return false;
        }

        if (messageId.HasValue
            && dbContext.GroupMessages.Any(storedMessage =>
                storedMessage.Id == messageId.Value))
        {
            errorMessage = "这条消息已经发送，请勿重复提交。";
            return false;
        }

        GroupMessage userMessage = new(
            groupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            trimmedContent,
            DateTime.Now,
            messageId: messageId,
            sequenceNumber: GetNextSequenceNumber(dbContext, groupChat.Id));

        dbContext.GroupMessages.Add(userMessage);
        dbContext.SaveChanges();

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

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "AI 回复内容不能为空。";
            return false;
        }

        string trimmedContent = content.Trim();

        if (trimmedContent.Length > GroupMessage.ContentMaxLength)
        {
            errorMessage = $"AI 回复内容不能超过 {GroupMessage.ContentMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.GroupChats.Any(storedGroupChat =>
                storedGroupChat.Id == groupChat.Id))
        {
            errorMessage = "群聊不存在，不能保存消息。";
            return false;
        }

        bool isMember = dbContext.GroupChats.Any(storedGroupChat =>
            storedGroupChat.Id == groupChat.Id
            && storedGroupChat.Members.Any(member => member.Id == aiSpeaker.Id));

        if (!isMember)
        {
            errorMessage = "未加入当前群聊的 AI 账号不能发送群消息。";
            return false;
        }

        GroupMessage aiMessage = new(
            groupChat.Id,
            MessageSenderType.AiAccount,
            aiSpeaker.Nickname,
            aiSpeaker.Id,
            trimmedContent,
            DateTime.Now,
            sequenceNumber: GetNextSequenceNumber(dbContext, groupChat.Id));

        dbContext.GroupMessages.Add(aiMessage);
        dbContext.SaveChanges();

        message = aiMessage;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 返回当前群聊按持久化会话序号排列的只读消息列表。
    /// </summary>
    public IReadOnlyList<GroupMessage> GetOrderedChatHistory(GroupChat groupChat)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        List<GroupMessage> orderedMessages = dbContext.GroupMessages
            .AsNoTracking()
            .Where(message => message.GroupChatId == groupChat.Id)
            .OrderBy(message => message.SequenceNumber)
            .ToList();

        return orderedMessages.AsReadOnly();
    }

    private static long GetNextSequenceNumber(
        VocaChatDbContext dbContext,
        Guid groupChatId)
    {
        return (dbContext.GroupMessages
            .Where(message => message.GroupChatId == groupChatId)
            .Max(message => (long?)message.SequenceNumber) ?? 0) + 1;
    }
}

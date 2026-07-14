using System;

namespace VocaChat.ConsoleApp.Models;

/// <summary>
/// 表示一条属于某个群聊的消息。
/// </summary>
public class GroupMessage
{
    internal const int SenderDisplayNameMaxLength = 100;
    internal const int ContentMaxLength = 4000;

    public Guid Id { get; private set; }
    public Guid GroupChatId { get; private set; }
    public MessageSenderType SenderType { get; private set; }
    public string SenderDisplayName { get; private set; }
    public Guid? SenderAiAccountId { get; private set; }
    public string Content { get; private set; }
    public DateTime SentAt { get; private set; }

    /// <summary>
    /// 供 EF Core 从数据库还原群消息使用。
    /// </summary>
    private GroupMessage()
    {
        SenderDisplayName = string.Empty;
        Content = string.Empty;
    }

    /// <summary>
    /// 创建群消息，并记录消息所属群聊、发送者和发送时间。
    /// </summary>
    public GroupMessage(
        Guid groupChatId,
        MessageSenderType senderType,
        string senderDisplayName,
        Guid? senderAiAccountId,
        string content)
        : this(
            groupChatId,
            senderType,
            senderDisplayName,
            senderAiAccountId,
            content,
            DateTime.Now)
    {
    }

    /// <summary>
    /// 使用指定发送时间创建群消息，供项目内部准备和验证消息顺序。
    /// </summary>
    internal GroupMessage(
        Guid groupChatId,
        MessageSenderType senderType,
        string senderDisplayName,
        Guid? senderAiAccountId,
        string content,
        DateTime sentAt)
    {
        Id = Guid.NewGuid();
        GroupChatId = groupChatId;
        SenderType = senderType;
        SenderDisplayName = senderDisplayName;
        SenderAiAccountId = senderAiAccountId;
        Content = content;
        SentAt = sentAt;
    }
}

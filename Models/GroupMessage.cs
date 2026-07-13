using System;

namespace VocaChat.ConsoleApp.Models;

/// <summary>
/// 表示一条属于某个群聊的消息。
/// </summary>
public class GroupMessage
{
    public Guid Id { get; }
    public Guid GroupChatId { get; }
    public MessageSenderType SenderType { get; }
    public string SenderDisplayName { get; }
    public Guid? SenderAiAccountId { get; }
    public string Content { get; }
    public DateTime SentAt { get; }

    /// <summary>
    /// 创建群消息，并记录消息所属群聊、发送者和发送时间。
    /// </summary>
    public GroupMessage(
        Guid groupChatId,
        MessageSenderType senderType,
        string senderDisplayName,
        Guid? senderAiAccountId,
        string content)
    {
        Id = Guid.NewGuid();
        GroupChatId = groupChatId;
        SenderType = senderType;
        SenderDisplayName = senderDisplayName;
        SenderAiAccountId = senderAiAccountId;
        Content = content;
        SentAt = DateTime.Now;
    }
}

using System;

namespace VocaChat.Models;

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
    public Guid? AutonomousGroupChatSessionId { get; private set; }
    public Guid? AutonomousGroupChatRoundId { get; private set; }
    public Guid? InteractionBatchId { get; private set; }
    public Guid? AiResponseBatchId { get; private set; }
    public Guid? ReplyToMessageId { get; private set; }
    public long SequenceNumber { get; private set; }
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
        DateTime sentAt,
        Guid? autonomousGroupChatSessionId = null,
        Guid? autonomousGroupChatRoundId = null,
        Guid? messageId = null,
        long sequenceNumber = 0,
        Guid? interactionBatchId = null,
        Guid? aiResponseBatchId = null,
        Guid? replyToMessageId = null)
    {
        Id = messageId ?? Guid.NewGuid();
        GroupChatId = groupChatId;
        SenderType = senderType;
        SenderDisplayName = senderDisplayName;
        SenderAiAccountId = senderAiAccountId;
        AutonomousGroupChatSessionId = autonomousGroupChatSessionId;
        AutonomousGroupChatRoundId = autonomousGroupChatRoundId;
        InteractionBatchId = interactionBatchId;
        AiResponseBatchId = aiResponseBatchId;
        ReplyToMessageId = replyToMessageId;
        SequenceNumber = sequenceNumber;
        Content = content;
        SentAt = sentAt;
    }
}

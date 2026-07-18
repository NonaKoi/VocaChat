namespace VocaChat.Models;

/// <summary>
/// 表示一条属于私聊的用户消息或 AI 好友消息。
/// </summary>
public class PrivateMessage
{
    internal const int SenderDisplayNameMaxLength = 100;
    internal const int ContentMaxLength = 4000;

    public Guid Id { get; private set; }
    public Guid PrivateChatId { get; private set; }
    public MessageSenderType SenderType { get; private set; }
    public string SenderDisplayName { get; private set; }
    public Guid? SenderAiAccountId { get; private set; }
    public string Content { get; private set; }
    public DateTime SentAt { get; private set; }

    private PrivateMessage()
    {
        SenderDisplayName = string.Empty;
        Content = string.Empty;
    }

    internal PrivateMessage(
        Guid privateChatId,
        MessageSenderType senderType,
        string senderDisplayName,
        Guid? senderAiAccountId,
        string content)
        : this(
            privateChatId,
            senderType,
            senderDisplayName,
            senderAiAccountId,
            content,
            DateTime.Now)
    {
    }

    /// <summary>
    /// 使用明确时间创建消息，供同一次自主交流稳定保存开场白和回复。
    /// </summary>
    internal PrivateMessage(
        Guid privateChatId,
        MessageSenderType senderType,
        string senderDisplayName,
        Guid? senderAiAccountId,
        string content,
        DateTime sentAt)
    {
        Id = Guid.NewGuid();
        PrivateChatId = privateChatId;
        SenderType = senderType;
        SenderDisplayName = senderDisplayName;
        SenderAiAccountId = senderAiAccountId;
        Content = content;
        SentAt = sentAt;
    }
}

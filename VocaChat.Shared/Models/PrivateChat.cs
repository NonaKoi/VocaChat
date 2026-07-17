namespace VocaChat.Models;

/// <summary>
/// 表示当前本地用户与一位好友之间长期存在的私聊。
/// </summary>
public class PrivateChat
{
    public Guid Id { get; private set; }
    public PrivateChatKind Kind { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? FirstAiAccountId { get; private set; }
    public Guid? SecondAiAccountId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Contact? Contact { get; private set; }
    public AiAccount? FirstAiAccount { get; private set; }
    public AiAccount? SecondAiAccount { get; private set; }

    private PrivateChat()
    {
    }

    internal PrivateChat(Guid contactId)
    {
        Id = Guid.NewGuid();
        Kind = PrivateChatKind.LocalUserAndAiAccount;
        ContactId = contactId;
        CreatedAt = DateTime.Now;
    }

    /// <summary>
    /// 创建两个既有 AI 账号之间的好友私信，并规范化参与者顺序以防止反向重复。
    /// </summary>
    internal PrivateChat(Guid firstAiAccountId, Guid secondAiAccountId)
    {
        Id = Guid.NewGuid();
        Kind = PrivateChatKind.AiAccounts;

        if (firstAiAccountId.CompareTo(secondAiAccountId) <= 0)
        {
            FirstAiAccountId = firstAiAccountId;
            SecondAiAccountId = secondAiAccountId;
        }
        else
        {
            FirstAiAccountId = secondAiAccountId;
            SecondAiAccountId = firstAiAccountId;
        }

        CreatedAt = DateTime.Now;
    }
}

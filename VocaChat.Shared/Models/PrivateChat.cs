namespace VocaChat.Models;

/// <summary>
/// 表示当前本地用户与一位好友之间长期存在的私聊。
/// </summary>
public class PrivateChat
{
    public Guid Id { get; private set; }
    public Guid ContactId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Contact Contact { get; private set; }

    private PrivateChat()
    {
        Contact = null!;
    }

    internal PrivateChat(Guid contactId)
    {
        Id = Guid.NewGuid();
        ContactId = contactId;
        CreatedAt = DateTime.Now;
        Contact = null!;
    }
}

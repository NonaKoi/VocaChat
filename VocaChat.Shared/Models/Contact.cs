namespace VocaChat.Models;

/// <summary>
/// 表示当前本地用户与一个长期存在的 AI 账号之间的好友关系。
/// </summary>
public class Contact
{
    public Guid Id { get; private set; }
    public Guid AiAccountId { get; private set; }
    public Guid ContactGroupId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public AiAccount AiAccount { get; private set; }
    public ContactGroup ContactGroup { get; private set; }

    private Contact()
    {
        AiAccount = null!;
        ContactGroup = null!;
    }

    internal Contact(Guid aiAccountId, Guid contactGroupId)
    {
        Id = Guid.NewGuid();
        AiAccountId = aiAccountId;
        ContactGroupId = contactGroupId;
        CreatedAt = DateTime.Now;
        AiAccount = null!;
        ContactGroup = null!;
    }

    internal void MoveToGroup(Guid contactGroupId)
    {
        ContactGroupId = contactGroupId;
    }
}

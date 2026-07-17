namespace VocaChat.Models;

/// <summary>
/// 表示当前本地用户用于整理好友的一个分组。
/// </summary>
public class ContactGroup
{
    internal const int NameMaxLength = 50;

    public static readonly Guid DefaultGroupId =
        Guid.Parse("6d53f7a8-2d25-4e8a-bbad-82dc67384f01");

    public const string DefaultGroupName = "默认分组";

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ContactGroup()
    {
        Name = string.Empty;
    }

    internal ContactGroup(string name, int sortOrder)
    {
        Id = Guid.NewGuid();
        Name = name;
        SortOrder = sortOrder;
        CreatedAt = DateTime.Now;
    }
}

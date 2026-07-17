namespace VocaChat.WebApi.Dtos.Contacts;

/// <summary>表示一个好友分组。</summary>
public sealed class ContactGroupResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public DateTime CreatedAt { get; init; }
}

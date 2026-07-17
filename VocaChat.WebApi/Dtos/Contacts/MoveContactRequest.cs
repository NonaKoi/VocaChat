namespace VocaChat.WebApi.Dtos.Contacts;

/// <summary>将好友移动到另一个分组所需的数据。</summary>
public sealed class MoveContactRequest
{
    public Guid ContactGroupId { get; init; }
}

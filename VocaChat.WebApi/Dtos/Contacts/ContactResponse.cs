using VocaChat.WebApi.Dtos.AiAccounts;

namespace VocaChat.WebApi.Dtos.Contacts;

/// <summary>表示好友关系、所属分组和对应好友档案。</summary>
public sealed class ContactResponse
{
    public Guid Id { get; init; }
    public Guid ContactGroupId { get; init; }
    public string ContactGroupName { get; init; } = string.Empty;
    public AiAccountResponse Friend { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}

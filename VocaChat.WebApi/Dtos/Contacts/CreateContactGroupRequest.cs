namespace VocaChat.WebApi.Dtos.Contacts;

/// <summary>创建好友分组所需的数据。</summary>
public sealed class CreateContactGroupRequest
{
    public string Name { get; init; } = string.Empty;
}

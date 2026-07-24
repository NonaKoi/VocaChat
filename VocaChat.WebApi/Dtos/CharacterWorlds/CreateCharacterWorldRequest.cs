namespace VocaChat.WebApi.Dtos.CharacterWorlds;

/// <summary>
/// 表示创建角色世界时允许提交的数据。
/// </summary>
public sealed class CreateCharacterWorldRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

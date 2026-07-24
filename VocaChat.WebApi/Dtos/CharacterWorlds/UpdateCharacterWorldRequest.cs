namespace VocaChat.WebApi.Dtos.CharacterWorlds;

/// <summary>
/// 表示修改角色世界时允许提交的数据。
/// </summary>
public sealed class UpdateCharacterWorldRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

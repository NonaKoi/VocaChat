namespace VocaChat.WebApi.Dtos.CharacterWorlds;

/// <summary>
/// 表示通过 HTTP 返回的一个角色世界。
/// </summary>
public sealed class CharacterWorldResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

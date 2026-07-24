using VocaChat.Models;
using VocaChat.WebApi.Dtos.CharacterWorlds;

namespace VocaChat.WebApi.Mapping;

/// <summary>
/// 将内部角色世界实体映射为稳定的 HTTP 响应。
/// </summary>
public static class CharacterWorldResponseMapper
{
    public static CharacterWorldResponse ToResponse(
        CharacterWorld characterWorld)
    {
        return new CharacterWorldResponse
        {
            Id = characterWorld.Id,
            Name = characterWorld.Name,
            Description = characterWorld.Description,
            CreatedAt = characterWorld.CreatedAt,
            UpdatedAt = characterWorld.UpdatedAt
        };
    }
}

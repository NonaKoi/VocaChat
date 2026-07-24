using System;
using VocaChat.WebApi.Dtos.CharacterWorlds;

namespace VocaChat.WebApi.Dtos.AiAccounts;

/// <summary>
/// 表示通过 HTTP 返回给客户端的 AI 账号数据。
/// </summary>
public sealed class AiAccountResponse
{
    public Guid Id { get; init; }
    public string VcNumber { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string IdentityDescription { get; init; } = string.Empty;
    public string Personality { get; init; } = string.Empty;
    public string SpeakingStyle { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public DateOnly? Birthday { get; init; }
    public int? Age { get; init; }
    public string? ZodiacSign { get; init; }
    public string Gender { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Occupation { get; init; } = string.Empty;
    public string Hometown { get; init; } = string.Empty;
    public string OnlineStatus { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string? CoverUrl { get; init; }
    public Guid CharacterWorldId { get; init; }
    public CharacterWorldResponse CharacterWorld { get; init; } = new();
    public IReadOnlyList<string> InterestTags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonalityTags { get; init; } = Array.Empty<string>();
    public DateTime CreatedAt { get; init; }
}

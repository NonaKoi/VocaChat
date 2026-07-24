namespace VocaChat.WebApi.Dtos.AiAccounts;

/// <summary>
/// 表示客户端创建 AI 账号时允许提交的 HTTP 请求数据。
/// </summary>
public sealed class CreateAiAccountRequest
{
    public string? Nickname { get; set; }
    public string? VcNumber { get; set; }
    public string? IdentityDescription { get; set; }
    public string? Personality { get; set; }
    public string? SpeakingStyle { get; set; }
    public string? Signature { get; set; }
    public DateOnly? Birthday { get; set; }
    public string? Gender { get; set; }
    public string? Location { get; set; }
    public string? Occupation { get; set; }
    public string? Hometown { get; set; }
    public string? OnlineStatus { get; set; }
    public Guid? CharacterWorldId { get; set; }
    public IReadOnlyList<string> InterestTags { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PersonalityTags { get; set; } = Array.Empty<string>();
}

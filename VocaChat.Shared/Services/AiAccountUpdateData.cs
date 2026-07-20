using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 汇总修改完整 AI 账号档案所需的数据；不包含系统主键、创建时间或媒体标识。
/// </summary>
public sealed class AiAccountUpdateData
{
    public string Nickname { get; init; } = string.Empty;
    public string VcNumber { get; init; } = string.Empty;
    public string IdentityDescription { get; init; } = string.Empty;
    public string Personality { get; init; } = string.Empty;
    public string SpeakingStyle { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public DateOnly? Birthday { get; init; }
    public AiAccountGender Gender { get; init; } = AiAccountGender.Unspecified;
    public string Location { get; init; } = string.Empty;
    public string Occupation { get; init; } = string.Empty;
    public string Hometown { get; init; } = string.Empty;
    public OnlineStatus OnlineStatus { get; init; } = OnlineStatus.Offline;
    public IReadOnlyCollection<string> InterestTags { get; init; } =
        Array.Empty<string>();
    public IReadOnlyCollection<string> PersonalityTags { get; init; } =
        Array.Empty<string>();
}

/// <summary>
/// 明确账号资料更新结果，避免 HTTP 层根据错误文案推断状态码。
/// </summary>
public enum AiAccountUpdateStatus
{
    Success,
    AccountNotFound,
    InvalidData,
    DuplicateNickname,
    DuplicateVcNumber,
    PersistenceFailed
}

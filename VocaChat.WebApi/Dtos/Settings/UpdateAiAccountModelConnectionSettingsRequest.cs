namespace VocaChat.WebApi.Dtos.Settings;

/// <summary>
/// 表示更新一个 AI 账号专有模型接口时提交的数据。
/// </summary>
public sealed record UpdateAiAccountModelConnectionSettingsRequest
{
    public bool UseGlobalSettings { get; init; } = true;
    public string? BaseUrl { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
    public bool ClearApiKey { get; init; }
}

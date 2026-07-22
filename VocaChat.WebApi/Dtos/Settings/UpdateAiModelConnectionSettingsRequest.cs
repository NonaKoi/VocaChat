namespace VocaChat.WebApi.Dtos.Settings;

/// <summary>
/// 表示更新全局模型接口时提交的数据；空白密钥表示保留现值。
/// </summary>
public sealed record UpdateAiModelConnectionSettingsRequest
{
    public string? BaseUrl { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
    public bool ClearApiKey { get; init; }
}

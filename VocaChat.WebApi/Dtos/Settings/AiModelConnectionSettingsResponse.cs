namespace VocaChat.WebApi.Dtos.Settings;

/// <summary>
/// 表示全局模型接口的安全摘要，不包含 API Key 原文。
/// </summary>
public sealed record AiModelConnectionSettingsResponse(
    string BaseUrl,
    string Model,
    bool HasApiKey);

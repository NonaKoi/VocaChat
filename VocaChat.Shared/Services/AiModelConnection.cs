namespace VocaChat.Services;

/// <summary>
/// 表示一次模型请求实际使用的完整连接信息。
/// </summary>
public sealed record AiModelConnection(
    string BaseUrl,
    string Model,
    string? ApiKey);

/// <summary>
/// 表示可安全返回给设置界面的全局连接状态，不包含 API Key 原文。
/// </summary>
public sealed record AiModelConnectionSettingsSnapshot(
    string BaseUrl,
    string Model,
    bool HasApiKey);

/// <summary>
/// 表示一个 AI 账号保存的专有连接设置及其当前实际生效状态。
/// </summary>
public sealed record AiAccountModelConnectionSettingsSnapshot(
    Guid AiAccountId,
    bool UseGlobalSettings,
    string BaseUrl,
    string Model,
    bool HasApiKey,
    string EffectiveBaseUrl,
    string EffectiveModel,
    bool EffectiveHasApiKey);

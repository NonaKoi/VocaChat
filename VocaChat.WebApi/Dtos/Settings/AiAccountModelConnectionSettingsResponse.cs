using System;

namespace VocaChat.WebApi.Dtos.Settings;

/// <summary>
/// 表示一个 AI 账号保存的专有接口及其当前实际生效状态。
/// </summary>
public sealed record AiAccountModelConnectionSettingsResponse(
    Guid AiAccountId,
    bool UseGlobalSettings,
    string BaseUrl,
    string Model,
    bool HasApiKey,
    string EffectiveBaseUrl,
    string EffectiveModel,
    bool EffectiveHasApiKey);

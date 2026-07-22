namespace VocaChat.Models;

/// <summary>
/// 保存一个 AI 账号是否沿用全局模型接口，以及它自己的完整连接设置。
/// </summary>
public class AiAccountModelConnectionSettings
{
    public Guid AiAccountId { get; private set; }
    public bool UseGlobalSettings { get; private set; }
    public string BaseUrl { get; private set; }
    public string Model { get; private set; }
    internal string? ProtectedApiKey { get; private set; }

    private AiAccountModelConnectionSettings()
    {
        BaseUrl = string.Empty;
        Model = string.Empty;
    }

    internal AiAccountModelConnectionSettings(
        Guid aiAccountId,
        string baseUrl,
        string model)
    {
        AiAccountId = aiAccountId;
        UseGlobalSettings = true;
        BaseUrl = baseUrl;
        Model = model;
    }

    internal void Update(
        bool useGlobalSettings,
        string baseUrl,
        string model,
        string? protectedApiKey)
    {
        UseGlobalSettings = useGlobalSettings;
        BaseUrl = baseUrl;
        Model = model;
        ProtectedApiKey = protectedApiKey;
    }
}

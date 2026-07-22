namespace VocaChat.Models;

/// <summary>
/// 保存本地用户统一使用的 OpenAI 兼容模型连接设置。
/// </summary>
public class AiModelConnectionSettings
{
    internal const int SingletonId = 1;

    public int Id { get; private set; }
    public string BaseUrl { get; private set; }
    public string Model { get; private set; }
    internal string? ProtectedApiKey { get; private set; }

    private AiModelConnectionSettings()
    {
        BaseUrl = string.Empty;
        Model = string.Empty;
    }

    internal AiModelConnectionSettings(
        string baseUrl,
        string model,
        string? protectedApiKey)
    {
        Id = SingletonId;
        BaseUrl = baseUrl;
        Model = model;
        ProtectedApiKey = protectedApiKey;
    }

    internal void Update(
        string baseUrl,
        string model,
        string? protectedApiKey)
    {
        BaseUrl = baseUrl;
        Model = model;
        ProtectedApiKey = protectedApiKey;
    }
}

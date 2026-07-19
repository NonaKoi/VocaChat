namespace VocaChat.Services;

/// <summary>
/// 保存可替换的 OpenAI 兼容模型连接参数，不与任何 AI 账号绑定。
/// </summary>
public sealed class AiMessageGenerationOptions
{
    public const string SectionName = "AiMessageGeneration";

    public string BaseUrl { get; set; } = "http://127.0.0.1:11434/v1/";
    public string Model { get; set; } = "vocachat-qwen3.5-4b";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public int RecentMessageLimit { get; set; } = 12;
    public int MaximumGeneratedMessageLength { get; set; } = 1000;
    public int MaximumCompletionTokens { get; set; } = 512;
    public int OutputValidationRetryCount { get; set; } = 1;
    public double Temperature { get; set; } = 0.7;
    public double TopP { get; set; } = 0.8;
}

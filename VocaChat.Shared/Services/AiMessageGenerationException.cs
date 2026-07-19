namespace VocaChat.Services;

/// <summary>
/// 表示模型连接失败或模型输出不符合消息契约。
/// </summary>
public sealed class AiMessageGenerationException : Exception
{
    public AiMessageGenerationException(string message)
        : base(message)
    {
    }

    public AiMessageGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

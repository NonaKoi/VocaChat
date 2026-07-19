namespace VocaChat.Services;

/// <summary>
/// 为已经由业务规则确定的发言者生成消息文本，不负责选择发言者、保存消息或修改关系。
/// </summary>
public interface IAiMessageGenerator
{
    Task<IReadOnlyList<string>> GenerateMessagesAsync(
        AiMessageGenerationRequest request,
        CancellationToken cancellationToken = default);
}

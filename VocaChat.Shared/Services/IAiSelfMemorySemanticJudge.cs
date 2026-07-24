namespace VocaChat.Services;

/// <summary>
/// 对已经通过硬规则预验证、且已有正式消息证据的记忆候选进行语义判断。
/// </summary>
public interface IAiSelfMemorySemanticJudge
{
    Task<AiSelfMemorySemanticJudgmentResult> JudgeAsync(
        AiSelfMemorySemanticJudgmentRequest request,
        CancellationToken cancellationToken = default);
}

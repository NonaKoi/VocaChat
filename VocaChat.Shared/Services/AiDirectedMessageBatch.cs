namespace VocaChat.Services;

/// <summary>
/// 将一次导演计划、经过身份规则清理的请求和最终生成文本保持在同一批次中。
/// </summary>
public sealed record AiDirectedMessageBatch(
    AiMessageGenerationRequest Request,
    AiIdentityContinuityPlan ContinuityPlan,
    IReadOnlyList<string> Contents);

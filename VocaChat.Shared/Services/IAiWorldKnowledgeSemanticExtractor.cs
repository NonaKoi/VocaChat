namespace VocaChat.Services;

/// <summary>
/// 在确定性规则无法识别时，仅依据一条正式消息的原文判断世界知识信号。
/// 实现不能读取角色完整资料，也不能把模型先验知识写入提取结果。
/// </summary>
public interface IAiWorldKnowledgeSemanticExtractor
{
    Task<AiWorldKnowledgeSemanticExtractionResult> ExtractAsync(
        AiWorldKnowledgeSemanticExtractionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供语义提取所需的最小上下文。消息原文是唯一事实来源。
/// </summary>
public sealed record AiWorldKnowledgeSemanticExtractionRequest(
    string Content,
    Guid? SourceAiAccountId,
    AiModelUsageCorrelation? UsageCorrelation);

/// <summary>
/// 模型从消息原文中识别出的概念名称和类别。名称必须原样出现在消息中。
/// </summary>
public sealed record AiWorldKnowledgeSemanticConcept(
    string Name,
    AiWorldKnowledgeConceptCategory Category);

public enum AiWorldKnowledgeConceptCategory
{
    Person,
    Place,
    School,
    Organization,
    Polity,
    Environment,
    Culture,
    Event,
    Concept
}

/// <summary>
/// 返回语义分类结果；失败只影响知识后处理，不撤销已经保存的消息。
/// </summary>
public sealed record AiWorldKnowledgeSemanticExtractionResult(
    AiWorldKnowledgeSignal Signal,
    IReadOnlyList<AiWorldKnowledgeSemanticConcept> Concepts,
    string? ErrorMessage)
{
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

    public static AiWorldKnowledgeSemanticExtractionResult None { get; } =
        new(
            AiWorldKnowledgeSignal.None,
            Array.Empty<AiWorldKnowledgeSemanticConcept>(),
            ErrorMessage: null);

    public static AiWorldKnowledgeSemanticExtractionResult Failed(
        string errorMessage) =>
        new(
            AiWorldKnowledgeSignal.None,
            Array.Empty<AiWorldKnowledgeSemanticConcept>(),
            errorMessage);
}

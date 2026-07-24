using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示从一条正式消息中提取出的、尚未经过 Owner 可见性验证的世界知识候选。
/// </summary>
public sealed record AiWorldKnowledgeCandidate(
    Guid SubjectCharacterWorldId,
    Guid SubjectAiAccountId,
    string KnowledgeKey,
    string Summary,
    AiWorldKnowledgeFactNature FactNature,
    AiWorldKnowledgeMutability Mutability,
    AiWorldKnowledgeTrustLevel TrustLevel,
    int Salience,
    AiWorldKnowledgeSignal Signal);

/// <summary>
/// 表示对一条消息只执行一次候选提取后的结构化结果。
/// </summary>
public sealed record AiWorldKnowledgeExtraction(
    AiWorldKnowledgeSignal Signal,
    IReadOnlyList<AiWorldKnowledgeCandidate> Candidates,
    bool UsedSemanticExtractor = false,
    string? ErrorMessage = null)
{
    public static AiWorldKnowledgeExtraction None { get; } =
        new(
            AiWorldKnowledgeSignal.None,
            Array.Empty<AiWorldKnowledgeCandidate>());
}

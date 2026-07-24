namespace VocaChat.WebApi.Dtos.AiWorldKnowledge;

/// <summary>
/// 表示一条世界知识对应的真实会话消息来源。
/// </summary>
public sealed class AiWorldKnowledgeEvidenceResponse
{
    public Guid EvidenceId { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public Guid? SourceAiAccountId { get; init; }
    public string SourceDisplayName { get; init; } = string.Empty;
    public string ConversationKind { get; init; } = string.Empty;
    public Guid ConversationId { get; init; }
    public string ConversationDisplayName { get; init; } = string.Empty;
    public Guid MessageId { get; init; }
    public string MessageContent { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
    public string EvidenceSummary { get; init; } = string.Empty;
}

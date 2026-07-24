namespace VocaChat.Models;

/// <summary>
/// 将一条世界知识关联到实际保存的私聊或群聊消息。
/// </summary>
public sealed class AiWorldKnowledgeEvidence
{
    internal const int EvidenceSummaryMaxLength = 1000;

    public Guid Id { get; private set; }
    public Guid AiWorldKnowledgeId { get; private set; }
    public MessageSenderType SourceType { get; private set; }
    public Guid? SourceAiAccountId { get; private set; }
    public Guid? SourcePrivateMessageId { get; private set; }
    public Guid? SourceGroupMessageId { get; private set; }
    public string EvidenceSummary { get; private set; }
    public DateTime ObservedAt { get; private set; }
    public AiWorldKnowledge AiWorldKnowledge { get; private set; }
    public AiAccount? SourceAiAccount { get; private set; }

    private AiWorldKnowledgeEvidence()
    {
        EvidenceSummary = string.Empty;
        AiWorldKnowledge = null!;
    }

    internal AiWorldKnowledgeEvidence(
        Guid aiWorldKnowledgeId,
        MessageSenderType sourceType,
        Guid? sourceAiAccountId,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        string evidenceSummary,
        DateTime observedAt)
    {
        Id = Guid.NewGuid();
        AiWorldKnowledgeId = aiWorldKnowledgeId;
        SourceType = sourceType;
        SourceAiAccountId = sourceAiAccountId;
        SourcePrivateMessageId = sourcePrivateMessageId;
        SourceGroupMessageId = sourceGroupMessageId;
        EvidenceSummary = evidenceSummary;
        ObservedAt = observedAt;
        AiWorldKnowledge = null!;
    }
}

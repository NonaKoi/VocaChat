using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示设置页可以查看的一条世界知识来源消息。
/// </summary>
public sealed record AiWorldKnowledgeEvidenceDetails(
    Guid EvidenceId,
    MessageSenderType SourceType,
    Guid? SourceAiAccountId,
    string SourceDisplayName,
    string ConversationKind,
    Guid ConversationId,
    string ConversationDisplayName,
    Guid MessageId,
    string MessageContent,
    DateTime SentAt,
    string EvidenceSummary);

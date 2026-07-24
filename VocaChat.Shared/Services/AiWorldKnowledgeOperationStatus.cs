namespace VocaChat.Services;

/// <summary>
/// 表示世界知识、来源证据和群消息接收者快照操作的明确结果。
/// </summary>
public enum AiWorldKnowledgeOperationStatus
{
    Success,
    AlreadyExists,
    EvidenceAdded,
    KnowledgeSuperseded,
    ConflictCandidateCreated,
    AccountNotFound,
    CharacterWorldNotFound,
    InvalidSubject,
    InvalidKnowledgeKey,
    InvalidSummary,
    InvalidClassification,
    InvalidSalience,
    InvalidLimit,
    InvalidSource,
    SourceNotFound,
    SourceNotVisible,
    SelfAuthoredSource,
    GroupMessageNotFound,
    KnowledgeNotFound,
    KnowledgeLocked,
    InvalidStatus,
    PersistenceFailed
}

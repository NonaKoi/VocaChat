namespace VocaChat.Services;

/// <summary>
/// 表示方向记忆创建或查询的明确业务结果。
/// </summary>
public enum AiMemoryOperationStatus
{
    Success,
    AlreadyExists,
    SelfMemoryNotAllowed,
    AccountNotFound,
    InvalidType,
    InvalidSummary,
    InvalidSalience,
    InvalidLimit,
    SourceNotFound,
    SourceMismatch,
    SessionNotEligible,
    PersistenceFailed
}

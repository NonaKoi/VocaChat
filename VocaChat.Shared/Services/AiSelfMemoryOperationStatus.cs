namespace VocaChat.Services;

/// <summary>
/// 表示个人记忆读取或保存操作的明确业务结果。
/// </summary>
public enum AiSelfMemoryOperationStatus
{
    Success,
    AlreadyExists,
    AccountNotFound,
    MemoryNotFound,
    InvalidType,
    InvalidFactKey,
    InvalidClassification,
    CharacterWorldNotFound,
    InvalidStatus,
    InvalidSummary,
    InvalidSalience,
    InvalidLimit,
    InvalidTimeRange,
    PersistenceFailed
}

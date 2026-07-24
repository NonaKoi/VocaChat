namespace VocaChat.Services;

/// <summary>
/// 表示账号级或方向性世界认知操作的明确业务结果。
/// </summary>
public enum AiWorldAwarenessOperationStatus
{
    Success,
    AccountNotFound,
    SameAccountNotAllowed,
    InvalidState,
    InvalidEvidenceCount,
    InvalidSource,
    SourceNotFound,
    SourceNotVisible,
    SelfAuthoredSource,
    UserLocked,
    StateRegressionNotAllowed,
    PersistenceFailed
}

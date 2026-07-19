namespace VocaChat.Services;

/// <summary>
/// 表示一次自主私信关系演化停在了哪个明确阶段。
/// </summary>
public enum RelationshipEvolutionStatus
{
    Success,
    SessionNotFound,
    SessionNotCompleted,
    SessionHasNoCompletedRounds,
    AlreadyApplied,
    InvalidAuditState,
    PersistenceFailed,
    NotApplied
}

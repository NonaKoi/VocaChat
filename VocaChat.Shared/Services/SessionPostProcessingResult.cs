using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示 Session 后处理最终停在了哪个阶段。
/// </summary>
public enum SessionPostProcessingStatus
{
    Success,
    SuccessWithFallback,
    AlreadyProcessed,
    SessionNotFound,
    SessionNotEligible,
    ParticipantNotFound,
    RelationshipPersistenceFailed,
    MemoryPersistencePartialFailure
}

/// <summary>
/// 返回关系和记忆的实际保存结果，且不把后处理失败伪装成消息失败。
/// </summary>
public sealed class SessionPostProcessingResult
{
    public SessionPostProcessingStatus Status { get; init; }
    public SessionInsightAnalysis? Analysis { get; init; }
    public IReadOnlyList<AiRelationshipChange> RelationshipChanges { get; init; } =
        Array.Empty<AiRelationshipChange>();
    public IReadOnlyList<AiMemory> Memories { get; init; } =
        Array.Empty<AiMemory>();
    public string Message { get; init; } = string.Empty;
}

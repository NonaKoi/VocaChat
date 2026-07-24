namespace VocaChat.WebApi.Dtos.AiWorldKnowledge;

/// <summary>
/// 表示账号对平行世界和其他好友世界的当前认识概览。
/// </summary>
public sealed class AiWorldAwarenessOverviewResponse
{
    public Guid AiAccountId { get; init; }
    public ParallelWorldAwarenessResponse ParallelWorld { get; init; } =
        new();
    public IReadOnlyList<WorldAwarenessSubjectResponse> Subjects { get; init; } =
        Array.Empty<WorldAwarenessSubjectResponse>();
}

public sealed class ParallelWorldAwarenessResponse
{
    public string State { get; init; } = string.Empty;
    public bool IsUserLocked { get; init; }
    public DateTime? FirstInformedAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class WorldAwarenessSubjectResponse
{
    public Guid AiAccountId { get; init; }
    public string Nickname { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public Guid CharacterWorldId { get; init; }
    public string CharacterWorldName { get; init; } = string.Empty;
    public string AwarenessState { get; init; } = string.Empty;
    public bool IsUserLocked { get; init; }
    public int AwarenessEvidenceCount { get; init; }
    public int AwarenessConversationCount { get; init; }
    public string FamiliarityLevel { get; init; } = string.Empty;
    public int ActiveKnowledgeCount { get; init; }
    public int DistinctTopicCount { get; init; }
    public int KnowledgeEvidenceCount { get; init; }
    public int KnowledgeConversationCount { get; init; }
}

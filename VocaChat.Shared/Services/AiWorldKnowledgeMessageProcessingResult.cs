namespace VocaChat.Services;

/// <summary>
/// 表示一条已保存消息的世界知识后处理结果。
/// </summary>
public enum AiWorldKnowledgeMessageProcessingStatus
{
    Success,
    NoRelevantKnowledge,
    AlreadyProcessed,
    MessageNotFound,
    PartialFailure,
    PersistenceFailed
}

/// <summary>
/// 返回本轮实际形成的知识、认知变化和非阻塞错误。
/// </summary>
public sealed record AiWorldKnowledgeMessageProcessingResult(
    AiWorldKnowledgeMessageProcessingStatus Status,
    IReadOnlyList<Guid> KnowledgeIds,
    IReadOnlyList<Guid> UpdatedObserverAiAccountIds,
    IReadOnlyList<Guid> NewlyInformedAiAccountIds,
    IReadOnlyList<string> Errors)
{
    public bool HasFailures =>
        Status is AiWorldKnowledgeMessageProcessingStatus.PartialFailure
            or AiWorldKnowledgeMessageProcessingStatus.PersistenceFailed;
}

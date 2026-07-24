namespace VocaChat.Services;

/// <summary>
/// 表示一条正式消息中能够影响跨世界认知的最小、可解释信号。
/// </summary>
public enum AiWorldKnowledgeSignal
{
    None,
    UnfamiliarConcept,
    BackgroundDifference,
    ParallelWorldInformation,
    ExplicitCrossWorldConfirmation
}

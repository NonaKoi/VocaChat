using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示当前认知阶段允许导演采用的跨世界询问深度。
/// </summary>
public enum AiWorldInquiryMode
{
    None,
    ClarifyUnfamiliarConcept,
    ExploreBackgroundDifference,
    DiscussConfirmedWorld
}

/// <summary>
/// 表示经过所有者、对象和状态校验后，允许进入当前发言者提示词的世界知识。
/// </summary>
public sealed record AiConversationWorldKnowledge(
    Guid Id,
    Guid OwnerAiAccountId,
    Guid SubjectCharacterWorldId,
    Guid? SubjectAiAccountId,
    string Summary,
    AiWorldKnowledgeTrustLevel TrustLevel,
    int Salience,
    DateTime UpdatedAt);

/// <summary>
/// 保存当前发言者自己的世界认知视角，不暴露数据库中的系统真实关系。
/// </summary>
public sealed record AiWorldConversationContext(
    AiParallelWorldAwarenessState ParallelWorldAwareness,
    AiWorldAwarenessState RelationshipAwareness,
    Guid? SubjectAiAccountId,
    Guid? SubjectCharacterWorldId,
    string? VisibleSubjectWorldName,
    bool IsNewlyInformedByCurrentMessage,
    AiWorldInquiryMode InquiryMode,
    IReadOnlyList<AiConversationWorldKnowledge> RelevantKnowledge)
{
    public bool KnowsParallelWorldsExist =>
        ParallelWorldAwareness != AiParallelWorldAwarenessState.Unaware;

    public bool CanNameSubjectWorld =>
        RelationshipAwareness == AiWorldAwarenessState.CrossWorldConfirmed
        && !string.IsNullOrWhiteSpace(VisibleSubjectWorldName);
}

/// <summary>
/// 保存当前发言者在群聊中面向多位成员时的认知视角。
/// 每个成员上下文仍然具有独立的方向状态和知识范围，不能互相借用。
/// </summary>
public sealed record AiGroupWorldConversationContext(
    AiParallelWorldAwarenessState ParallelWorldAwareness,
    bool IsNewlyInformedByCurrentMessage,
    IReadOnlyList<AiWorldConversationContext> ParticipantContexts)
{
    public bool KnowsParallelWorldsExist =>
        ParallelWorldAwareness != AiParallelWorldAwarenessState.Unaware;

    public AiWorldConversationContext? FindParticipant(Guid aiAccountId) =>
        ParticipantContexts.SingleOrDefault(context =>
            context.SubjectAiAccountId == aiAccountId);
}

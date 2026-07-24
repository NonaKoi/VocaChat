using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 保存一次实时派生的世界熟悉度及其可审计计数。
/// </summary>
public sealed record AiWorldFamiliarity(
    AiWorldFamiliarityLevel Level,
    int ActiveKnowledgeCount,
    int DistinctTopicCount,
    int EvidenceCount,
    int DistinctConversationCount);

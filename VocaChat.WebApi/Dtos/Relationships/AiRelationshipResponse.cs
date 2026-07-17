using System;

namespace VocaChat.WebApi.Dtos.Relationships;

/// <summary>表示一个好友对另一个好友形成的有方向关系。</summary>
public sealed class AiRelationshipResponse
{
    public Guid FromAiAccountId { get; init; }
    public Guid ToAiAccountId { get; init; }
    public int Familiarity { get; init; }
    public int Affinity { get; init; }
    public int Trust { get; init; }
    public int InteractionCount { get; init; }
    public DateTime? LastInteractionAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

namespace VocaChat.WebApi.Dtos.AiSelfMemories;

/// <summary>表示一个 AI 账号自身的持久化个人记忆。</summary>
public sealed class AiSelfMemoryResponse
{
    public Guid Id { get; init; }
    public Guid AiAccountId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string FactKey { get; init; } = string.Empty;
    public string FactNature { get; init; } = string.Empty;
    public string Mutability { get; init; } = string.Empty;
    public string TrustLevel { get; init; } = string.Empty;
    public Guid CharacterWorldId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Salience { get; init; }
    public bool IsUserLocked { get; init; }
    public Guid? SourceConversationId { get; init; }
    public Guid? SourceMessageId { get; init; }
    public Guid? SupersedesMemoryId { get; init; }
    public DateTime? OccurredAt { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

namespace VocaChat.WebApi.Dtos.AiWorldKnowledge;

/// <summary>
/// 表示一个账号持有的一条可管理世界知识。
/// </summary>
public sealed class AiWorldKnowledgeResponse
{
    public Guid Id { get; init; }
    public Guid OwnerAiAccountId { get; init; }
    public Guid SubjectCharacterWorldId { get; init; }
    public Guid? SubjectAiAccountId { get; init; }
    public string KnowledgeKey { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string FactNature { get; init; } = string.Empty;
    public string Mutability { get; init; } = string.Empty;
    public string TrustLevel { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Salience { get; init; }
    public bool IsUserLocked { get; init; }
    public int EvidenceCount { get; init; }
    public DateTime FirstLearnedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

namespace VocaChat.Models;

/// <summary>
/// 保存一个 AI 账号从对话中获得的、具有世界作用域的方向性知识。
/// </summary>
public sealed class AiWorldKnowledge
{
    internal const int KnowledgeKeyMaxLength = 160;
    internal const int SummaryMaxLength = 1000;
    internal const int MinimumSalience = 1;
    internal const int MaximumSalience = 100;

    public Guid Id { get; private set; }
    public Guid OwnerAiAccountId { get; private set; }
    public Guid SubjectCharacterWorldId { get; private set; }
    public Guid? SubjectAiAccountId { get; private set; }
    public string KnowledgeKey { get; private set; }
    public string Summary { get; private set; }
    public AiWorldKnowledgeFactNature FactNature { get; private set; }
    public AiWorldKnowledgeMutability Mutability { get; private set; }
    public AiWorldKnowledgeTrustLevel TrustLevel { get; private set; }
    public AiWorldKnowledgeStatus Status { get; private set; }
    public int Salience { get; private set; }
    public bool IsUserLocked { get; private set; }
    public DateTime FirstLearnedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public AiAccount OwnerAiAccount { get; private set; }
    public CharacterWorld SubjectCharacterWorld { get; private set; }
    public AiAccount? SubjectAiAccount { get; private set; }

    private AiWorldKnowledge()
    {
        KnowledgeKey = string.Empty;
        Summary = string.Empty;
        OwnerAiAccount = null!;
        SubjectCharacterWorld = null!;
    }

    internal AiWorldKnowledge(
        Guid ownerAiAccountId,
        Guid subjectCharacterWorldId,
        Guid? subjectAiAccountId,
        string knowledgeKey,
        string summary,
        AiWorldKnowledgeFactNature factNature,
        AiWorldKnowledgeMutability mutability,
        AiWorldKnowledgeTrustLevel trustLevel,
        int salience,
        bool isUserLocked,
        DateTime learnedAt)
    {
        Id = Guid.NewGuid();
        OwnerAiAccountId = ownerAiAccountId;
        SubjectCharacterWorldId = subjectCharacterWorldId;
        SubjectAiAccountId = subjectAiAccountId;
        KnowledgeKey = knowledgeKey;
        Summary = summary;
        FactNature = factNature;
        Mutability = mutability;
        TrustLevel = trustLevel;
        Status = AiWorldKnowledgeStatus.Active;
        Salience = salience;
        IsUserLocked = isUserLocked;
        FirstLearnedAt = learnedAt;
        UpdatedAt = learnedAt;
        OwnerAiAccount = null!;
        SubjectCharacterWorld = null!;
    }

    /// <summary>
    /// 用户锁定或解除锁定一条仍然有效的世界知识。
    /// </summary>
    internal void SetUserLock(bool isUserLocked, DateTime updatedAt)
    {
        IsUserLocked = isUserLocked;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 在不改写知识内容的前提下，根据独立来源提升可信度。
    /// 用户已经确认的可信度不会被自动流程降低。
    /// </summary>
    internal void PromoteTrust(
        AiWorldKnowledgeTrustLevel trustLevel,
        DateTime updatedAt)
    {
        if (trustLevel > TrustLevel)
        {
            TrustLevel = trustLevel;
            UpdatedAt = updatedAt;
        }
    }

    /// <summary>
    /// 将恒定客观信息的相互矛盾版本保留为待确认候选。
    /// </summary>
    internal void MarkAsConflictCandidate(DateTime updatedAt)
    {
        Status = AiWorldKnowledgeStatus.ConflictCandidate;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 将已经被新版本替代的知识保留在历史中。
    /// </summary>
    internal void MarkAsSuperseded(DateTime updatedAt)
    {
        Status = AiWorldKnowledgeStatus.Superseded;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 用户修订一条有效知识，并将其明确标记为用户确认内容。
    /// 来源证据继续保留，不会因修订而丢失。
    /// </summary>
    internal void ReviseByUser(
        string summary,
        AiWorldKnowledgeFactNature factNature,
        AiWorldKnowledgeMutability mutability,
        int salience,
        bool isUserLocked,
        DateTime updatedAt)
    {
        Summary = summary;
        FactNature = factNature;
        Mutability = mutability;
        TrustLevel = AiWorldKnowledgeTrustLevel.UserConfirmed;
        Salience = salience;
        IsUserLocked = isUserLocked;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 用户可以先修正冲突候选的内容，再决定是否将其确认为有效版本。
    /// </summary>
    internal void ReviseConflictCandidate(
        string summary,
        AiWorldKnowledgeFactNature factNature,
        AiWorldKnowledgeMutability mutability,
        int salience,
        DateTime updatedAt)
    {
        Summary = summary;
        FactNature = factNature;
        Mutability = mutability;
        Salience = salience;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 用户确认当前版本。冲突候选的状态切换由 Service 在处理现有
    /// Active 版本之后显式完成。
    /// </summary>
    internal void ConfirmByUser(
        bool isUserLocked,
        DateTime updatedAt)
    {
        Status = AiWorldKnowledgeStatus.Active;
        TrustLevel = AiWorldKnowledgeTrustLevel.UserConfirmed;
        IsUserLocked = isUserLocked;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 将错误或不再需要的世界知识归档，不删除原始内容和证据。
    /// </summary>
    internal void ArchiveByUser(DateTime updatedAt)
    {
        Status = AiWorldKnowledgeStatus.Archived;
        UpdatedAt = updatedAt;
    }
}

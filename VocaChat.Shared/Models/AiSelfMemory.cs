namespace VocaChat.Models;

/// <summary>
/// 保存一个 AI 账号自己的经历、近况和计划；不表示该账号对其他好友的方向记忆。
/// </summary>
public sealed class AiSelfMemory
{
    internal const int SummaryMaxLength = 500;
    internal const int FactKeyMaxLength = 100;
    internal const int MinimumSalience = 1;
    internal const int MaximumSalience = 100;

    public Guid Id { get; private set; }
    public Guid AiAccountId { get; private set; }
    public AiSelfMemoryType Type { get; private set; }
    public string Summary { get; private set; }
    public string FactKey { get; private set; }
    public AiSelfMemoryFactNature FactNature { get; private set; }
    public AiSelfMemoryMutability Mutability { get; private set; }
    public AiSelfMemoryTrustLevel TrustLevel { get; private set; }
    public Guid CharacterWorldId { get; private set; }
    public AiSelfMemorySource Source { get; private set; }
    public AiSelfMemoryStatus Status { get; private set; }
    public int Salience { get; private set; }
    public bool IsUserLocked { get; private set; }
    public Guid? SourceConversationId { get; private set; }
    public Guid? SourceMessageId { get; private set; }
    public Guid? SupersedesMemoryId { get; private set; }
    public DateTime? OccurredAt { get; private set; }
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public AiAccount AiAccount { get; private set; }
    public CharacterWorld CharacterWorld { get; private set; }
    public AiSelfMemory? SupersedesMemory { get; private set; }

    private AiSelfMemory()
    {
        Summary = string.Empty;
        FactKey = string.Empty;
        AiAccount = null!;
        CharacterWorld = null!;
    }

    internal AiSelfMemory(
        Guid aiAccountId,
        AiSelfMemoryType type,
        string summary,
        string factKey,
        AiSelfMemoryFactNature factNature,
        AiSelfMemoryMutability mutability,
        AiSelfMemoryTrustLevel trustLevel,
        Guid characterWorldId,
        AiSelfMemorySource source,
        int salience,
        bool isUserLocked,
        Guid? sourceConversationId,
        Guid? sourceMessageId,
        Guid? supersedesMemoryId,
        DateTime? occurredAt,
        DateTime? validFrom,
        DateTime? validUntil,
        DateTime createdAt)
    {
        Id = Guid.NewGuid();
        AiAccountId = aiAccountId;
        Type = type;
        Summary = summary;
        FactKey = factKey;
        FactNature = factNature;
        Mutability = mutability;
        TrustLevel = trustLevel;
        CharacterWorldId = characterWorldId;
        Source = source;
        Status = AiSelfMemoryStatus.Active;
        Salience = salience;
        IsUserLocked = isUserLocked;
        SourceConversationId = sourceConversationId;
        SourceMessageId = sourceMessageId;
        SupersedesMemoryId = supersedesMemoryId;
        OccurredAt = occurredAt;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        AiAccount = null!;
        CharacterWorld = null!;
    }

    /// <summary>
    /// 用户确认新版本后将旧记录标记为已替代，原始内容和来源保持不变。
    /// </summary>
    internal void SupersedeByUser(DateTime updatedAt)
    {
        Status = AiSelfMemoryStatus.Superseded;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 归档或恢复一条个人记忆；Superseded 状态留给后续导演替代流程。
    /// </summary>
    internal void ChangeUserManagedStatus(
        AiSelfMemoryStatus status,
        DateTime updatedAt)
    {
        Status = status;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 导演以新事实替代旧记忆时保留旧记录，不覆盖其原始内容和来源。
    /// </summary>
    internal void SupersedeByDirector(DateTime updatedAt)
    {
        Status = AiSelfMemoryStatus.Superseded;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 导演确认一项动态事实已经不再有效时，将其归档而不是删除。
    /// </summary>
    internal void ArchiveByDirector(DateTime updatedAt)
    {
        Status = AiSelfMemoryStatus.Archived;
        UpdatedAt = updatedAt;
    }
}

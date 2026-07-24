namespace VocaChat.Models;

/// <summary>
/// 保存一个 AI 账号对另一个 AI 账号所处世界差异的方向性认知。
/// 没有持久化记录时，业务层将其视为
/// <see cref="AiWorldAwarenessState.AssumedSharedWorld" />。
/// </summary>
public sealed class AiWorldAwareness
{
    public Guid Id { get; private set; }
    public Guid ObserverAiAccountId { get; private set; }
    public Guid SubjectAiAccountId { get; private set; }
    public Guid SubjectCharacterWorldId { get; private set; }
    public AiWorldAwarenessState State { get; private set; }
    public int EvidenceCount { get; private set; }
    public int DistinctConversationCount { get; private set; }
    public DateTime? FirstEvidenceAt { get; private set; }
    public DateTime? LastEvidenceAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public Guid? LastSourcePrivateMessageId { get; private set; }
    public Guid? LastSourceGroupMessageId { get; private set; }
    public bool IsUserLocked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public AiAccount ObserverAiAccount { get; private set; }
    public AiAccount SubjectAiAccount { get; private set; }
    public CharacterWorld SubjectCharacterWorld { get; private set; }

    private AiWorldAwareness()
    {
        ObserverAiAccount = null!;
        SubjectAiAccount = null!;
        SubjectCharacterWorld = null!;
    }

    internal AiWorldAwareness(
        Guid observerAiAccountId,
        Guid subjectAiAccountId,
        Guid subjectCharacterWorldId,
        AiWorldAwarenessState state,
        int evidenceCount,
        int distinctConversationCount,
        DateTime? firstEvidenceAt,
        DateTime? lastEvidenceAt,
        DateTime? confirmedAt,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        bool isUserLocked,
        DateTime createdAt)
    {
        Id = Guid.NewGuid();
        ObserverAiAccountId = observerAiAccountId;
        SubjectAiAccountId = subjectAiAccountId;
        SubjectCharacterWorldId = subjectCharacterWorldId;
        State = state;
        EvidenceCount = evidenceCount;
        DistinctConversationCount = distinctConversationCount;
        FirstEvidenceAt = firstEvidenceAt;
        LastEvidenceAt = lastEvidenceAt;
        ConfirmedAt = confirmedAt;
        LastSourcePrivateMessageId = sourcePrivateMessageId;
        LastSourceGroupMessageId = sourceGroupMessageId;
        IsUserLocked = isUserLocked;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        ObserverAiAccount = null!;
        SubjectAiAccount = null!;
        SubjectCharacterWorld = null!;
    }

    /// <summary>
    /// 更新方向性认知；调用方必须先验证状态转换、证据和用户锁定规则。
    /// </summary>
    internal void Update(
        Guid subjectCharacterWorldId,
        AiWorldAwarenessState state,
        int evidenceCount,
        int distinctConversationCount,
        DateTime? firstEvidenceAt,
        DateTime? lastEvidenceAt,
        DateTime? confirmedAt,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        bool isUserLocked,
        DateTime updatedAt)
    {
        SubjectCharacterWorldId = subjectCharacterWorldId;
        State = state;
        EvidenceCount = evidenceCount;
        DistinctConversationCount = distinctConversationCount;
        FirstEvidenceAt = firstEvidenceAt;
        LastEvidenceAt = lastEvidenceAt;
        ConfirmedAt = confirmedAt;

        if (sourcePrivateMessageId.HasValue
            || sourceGroupMessageId.HasValue)
        {
            LastSourcePrivateMessageId = sourcePrivateMessageId;
            LastSourceGroupMessageId = sourceGroupMessageId;
        }

        IsUserLocked = isUserLocked;
        UpdatedAt = updatedAt;
    }
}

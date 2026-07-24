namespace VocaChat.Models;

/// <summary>
/// 保存一个 AI 账号是否已经知道平行世界可能存在。
/// 没有持久化记录时，业务层将其视为 <see cref="AiParallelWorldAwarenessState.Unaware" />。
/// </summary>
public sealed class AiParallelWorldAwareness
{
    public Guid Id { get; private set; }
    public Guid AiAccountId { get; private set; }
    public AiParallelWorldAwarenessState State { get; private set; }
    public DateTime? FirstInformedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid? LastSourcePrivateMessageId { get; private set; }
    public Guid? LastSourceGroupMessageId { get; private set; }
    public bool IsUserLocked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public AiAccount AiAccount { get; private set; }

    private AiParallelWorldAwareness()
    {
        AiAccount = null!;
    }

    internal AiParallelWorldAwareness(
        Guid aiAccountId,
        AiParallelWorldAwarenessState state,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        bool isUserLocked,
        DateTime createdAt)
    {
        Id = Guid.NewGuid();
        AiAccountId = aiAccountId;
        State = state;
        FirstInformedAt = state == AiParallelWorldAwarenessState.Unaware
            ? null
            : createdAt;
        AcceptedAt = state == AiParallelWorldAwarenessState.Accepted
            ? createdAt
            : null;
        LastSourcePrivateMessageId = sourcePrivateMessageId;
        LastSourceGroupMessageId = sourceGroupMessageId;
        IsUserLocked = isUserLocked;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        AiAccount = null!;
    }

    /// <summary>
    /// 更新账号级元认知；调用方必须先完成来源、锁定和状态转换验证。
    /// </summary>
    internal void Update(
        AiParallelWorldAwarenessState state,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        bool isUserLocked,
        DateTime updatedAt)
    {
        State = state;

        if (state == AiParallelWorldAwarenessState.Unaware)
        {
            FirstInformedAt = null;
            AcceptedAt = null;
        }
        else if (FirstInformedAt is null)
        {
            FirstInformedAt = updatedAt;
        }

        if (state == AiParallelWorldAwarenessState.Accepted
            && AcceptedAt is null)
        {
            AcceptedAt = updatedAt;
        }
        else if (state == AiParallelWorldAwarenessState.Informed)
        {
            AcceptedAt = null;
        }

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

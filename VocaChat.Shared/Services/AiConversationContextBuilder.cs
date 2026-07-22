using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 区分一条历史消息相对于当前发言账号的事实归属。
/// </summary>
public enum AiConversationMessageOwnership
{
    CurrentSpeaker,
    ReplyTargetAiAccount,
    OtherAiAccount,
    LocalUser
}

/// <summary>
/// 表示一条已经完成身份归属判定的生成上下文消息。
/// </summary>
public sealed record AiConversationContextMessage(
    AiDialogueMessage Message,
    AiConversationMessageOwnership Ownership);

/// <summary>
/// 保存按照原始时间顺序排列、但具有明确身份归属的最近消息。
/// </summary>
public sealed class AiConversationContext
{
    public AiConversationContextMessage? ReplyTarget { get; }
    public IReadOnlyList<AiConversationContextMessage> Messages { get; }
    public IReadOnlyList<AiConversationMemory> Memories { get; }
    public IReadOnlyList<AiConversationSelfMemory> SelfMemories { get; }
    public TimeSpan? GapSincePreviousMessage { get; }

    internal AiConversationContext(
        AiConversationContextMessage? replyTarget,
        IReadOnlyList<AiConversationContextMessage> messages,
        IReadOnlyList<AiConversationMemory> memories,
        IReadOnlyList<AiConversationSelfMemory> selfMemories,
        TimeSpan? gapSincePreviousMessage)
    {
        ReplyTarget = replyTarget;
        Messages = messages;
        Memories = memories;
        SelfMemories = selfMemories;
        GapSincePreviousMessage = gapSincePreviousMessage;
    }
}

/// <summary>
/// 使用发送者类型和账号 Id 构建身份隔离的模型上下文。
/// </summary>
public sealed class AiConversationContextBuilder
{
    private const int MaximumMemoryCount = 4;
    private const int MaximumSelfMemoryCount = 6;

    public AiConversationContext Build(
        AiMessageGenerationRequest request,
        int recentMessageLimit)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (recentMessageLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recentMessageLimit));
        }

        AiDialogueMessage? replyTargetMessage = request.ReplyTarget?.Message;
        Guid? relationshipTargetId = request.RelationshipTarget?.Id
            ?? replyTargetMessage?.SenderAiAccountId;
        AiConversationContextMessage? replyTarget = replyTargetMessage is null
            ? null
            : new AiConversationContextMessage(
                replyTargetMessage,
                ResolveOwnership(
                    replyTargetMessage,
                    request.Speaker.Id,
                    relationshipTargetId));

        List<AiConversationContextMessage> messages = request.RecentMessages
            .Where(message => !IsSameMessage(message, replyTargetMessage))
            .TakeLast(recentMessageLimit)
            .Select(message => new AiConversationContextMessage(
                message,
                ResolveOwnership(
                    message,
                    request.Speaker.Id,
                    relationshipTargetId)))
            .ToList();
        TimeSpan? gapSincePreviousMessage = CalculateGapSincePreviousMessage(
            replyTargetMessage,
            request.RecentMessages);

        HashSet<Guid> participantIds = request.OtherParticipants
            .Select(participant => participant.Id)
            .ToHashSet();
        List<AiConversationMemory> memories = request.RelevantMemories
            .Where(memory =>
                memory.OwnerAiAccountId == request.Speaker.Id
                && relationshipTargetId is not null
                && memory.SubjectAiAccountId == relationshipTargetId
                && participantIds.Contains(memory.SubjectAiAccountId)
                && !string.IsNullOrWhiteSpace(memory.Summary))
            .Take(MaximumMemoryCount)
            .ToList();
        List<AiConversationSelfMemory> selfMemories = request
            .RelevantSelfMemories
            .Where(memory =>
                memory.AiAccountId == request.Speaker.Id
                && !string.IsNullOrWhiteSpace(memory.Summary))
            .Take(MaximumSelfMemoryCount)
            .ToList();

        return new AiConversationContext(
            replyTarget,
            messages.AsReadOnly(),
            memories.AsReadOnly(),
            selfMemories.AsReadOnly(),
            gapSincePreviousMessage);
    }

    private static TimeSpan? CalculateGapSincePreviousMessage(
        AiDialogueMessage? replyTarget,
        IReadOnlyList<AiDialogueMessage> recentMessages)
    {
        if (replyTarget is null || replyTarget.SentAt == default)
        {
            return null;
        }

        DateTime? previousSentAt = recentMessages
            .Where(message =>
                !IsSameMessage(message, replyTarget)
                && message.SentAt != default
                && message.SentAt < replyTarget.SentAt)
            .Select(message => (DateTime?)message.SentAt)
            .Max();

        if (previousSentAt is null)
        {
            return null;
        }

        TimeSpan gap = replyTarget.SentAt - previousSentAt.Value;
        return gap > TimeSpan.Zero ? gap : null;
    }

    private static bool IsSameMessage(
        AiDialogueMessage message,
        AiDialogueMessage? other)
    {
        if (other is null)
        {
            return false;
        }

        if (message.MessageId != Guid.Empty && other.MessageId != Guid.Empty)
        {
            return message.MessageId == other.MessageId;
        }

        return message == other;
    }

    private static AiConversationMessageOwnership ResolveOwnership(
        AiDialogueMessage message,
        Guid currentSpeakerId,
        Guid? relationshipTargetId)
    {
        if (message.SenderType == MessageSenderType.User)
        {
            return AiConversationMessageOwnership.LocalUser;
        }

        if (message.SenderAiAccountId == currentSpeakerId)
        {
            return AiConversationMessageOwnership.CurrentSpeaker;
        }

        return relationshipTargetId is not null
               && message.SenderAiAccountId == relationshipTargetId
            ? AiConversationMessageOwnership.ReplyTargetAiAccount
            : AiConversationMessageOwnership.OtherAiAccount;
    }
}

/// <summary>
/// 将消息时间差转换为模型容易理解、但不过度精确的对话时间语义。
/// </summary>
internal static class AiConversationTimeGapFormatter
{
    public static string Format(TimeSpan? gap)
    {
        if (gap is null)
        {
            return "未知（不要自行假定刚刚连续聊过）";
        }

        if (gap.Value < TimeSpan.FromMinutes(5))
        {
            return "不足 5 分钟，属于连续交流";
        }

        if (gap.Value < TimeSpan.FromHours(1))
        {
            return $"约 {Math.Max(5, (int)Math.Round(gap.Value.TotalMinutes / 5) * 5)} 分钟";
        }

        if (gap.Value < TimeSpan.FromHours(24))
        {
            return $"约 {Math.Max(1, (int)Math.Round(gap.Value.TotalHours))} 小时";
        }

        return $"约 {Math.Max(1, (int)Math.Round(gap.Value.TotalDays))} 天，属于跨时间续聊";
    }
}

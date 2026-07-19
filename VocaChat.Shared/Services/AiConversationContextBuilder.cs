using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 区分一条历史消息相对于当前发言账号的事实归属。
/// </summary>
public enum AiConversationMessageOwnership
{
    CurrentSpeaker,
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

    internal AiConversationContext(
        AiConversationContextMessage? replyTarget,
        IReadOnlyList<AiConversationContextMessage> messages)
    {
        ReplyTarget = replyTarget;
        Messages = messages;
    }
}

/// <summary>
/// 使用发送者类型和账号 Id 构建身份隔离的模型上下文。
/// </summary>
public sealed class AiConversationContextBuilder
{
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
        AiConversationContextMessage? replyTarget = replyTargetMessage is null
            ? null
            : new AiConversationContextMessage(
                replyTargetMessage,
                ResolveOwnership(replyTargetMessage, request.Speaker.Id));

        List<AiConversationContextMessage> messages = request.RecentMessages
            .Where(message => !IsSameMessage(message, replyTargetMessage))
            .TakeLast(recentMessageLimit)
            .Select(message => new AiConversationContextMessage(
                message,
                ResolveOwnership(message, request.Speaker.Id)))
            .ToList();

        return new AiConversationContext(replyTarget, messages.AsReadOnly());
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
        Guid currentSpeakerId)
    {
        if (message.SenderType == MessageSenderType.User)
        {
            return AiConversationMessageOwnership.LocalUser;
        }

        return message.SenderAiAccountId == currentSpeakerId
            ? AiConversationMessageOwnership.CurrentSpeaker
            : AiConversationMessageOwnership.OtherAiAccount;
    }
}

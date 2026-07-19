using VocaChat.Models;

namespace VocaChat.Services;

public enum AiMessageGenerationScenario
{
    UserPrivateChat,
    GroupPrimaryReply,
    GroupFollowUpReply,
    AutonomousPrivateChat,
    AutonomousPrivateChatClosing
}

/// <summary>
/// 表示一条提供给消息生成器的最近聊天记录快照。
/// </summary>
public sealed record AiDialogueMessage(
    string SenderDisplayName,
    string Content,
    MessageSenderType SenderType,
    Guid? SenderAiAccountId,
    Guid MessageId = default,
    DateTime SentAt = default);

/// <summary>
/// 区分当前生成是在回应一条具体消息、开启话题，还是收束对话。
/// </summary>
public enum AiDialogueReplyTargetKind
{
    Message,
    TopicOpening,
    TopicContinuation,
    ConversationClosing
}

/// <summary>
/// 明确本轮生成必须完成的对话目标，避免模型在历史记录中自行猜测回应对象。
/// </summary>
public sealed record AiDialogueReplyTarget(
    AiDialogueReplyTargetKind Kind,
    AiDialogueMessage? Message = null)
{
    public static AiDialogueReplyTarget ReplyTo(AiDialogueMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new AiDialogueReplyTarget(
            AiDialogueReplyTargetKind.Message,
            message);
    }

    public static AiDialogueReplyTarget OpenTopic() =>
        new(AiDialogueReplyTargetKind.TopicOpening);

    public static AiDialogueReplyTarget ContinueTopic() =>
        new(AiDialogueReplyTargetKind.TopicContinuation);

    public static AiDialogueReplyTarget CloseConversation(
        AiDialogueMessage? message) =>
        new(AiDialogueReplyTargetKind.ConversationClosing, message);
}

/// <summary>
/// 描述一次已由业务层规划完成的文本生成请求。
/// </summary>
public sealed record AiMessageGenerationRequest
{
    public required AiMessageGenerationScenario Scenario { get; init; }
    public required AiAccount Speaker { get; init; }
    public IReadOnlyList<AiAccount> OtherParticipants { get; init; } =
        Array.Empty<AiAccount>();
    public AiAccount? PrimarySpeaker { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string FocusContent { get; init; } = string.Empty;
    public AiDialogueReplyTarget? ReplyTarget { get; init; }
    /// <summary>
    /// 保存整轮互动最初的用户消息；群聊后续发言仍需同时遵守其中未完成的要求。
    /// </summary>
    public AiDialogueMessage? ConversationAnchor { get; init; }
    public IReadOnlyList<AiDialogueMessage> RecentMessages { get; init; } =
        Array.Empty<AiDialogueMessage>();
    public int ExpectedMessageCount { get; init; } = 1;
    public int? RoundNumber { get; init; }
    public bool IsInitiator { get; init; }
    /// <summary>
    /// 仅用于自主交流，明确对方是否已在当前 Session 实际发言，防止替沉默者虚构观点。
    /// </summary>
    public bool? OtherParticipantHasResponded { get; init; }
    public double? SpeakerToOtherRelationshipScore { get; init; }
    public double? OtherToSpeakerRelationshipScore { get; init; }
    public ConversationActionPlan? ActionPlan { get; init; }
    public ConversationDirectionPlan? DirectionPlan { get; init; }
}

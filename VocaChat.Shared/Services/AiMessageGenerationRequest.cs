using VocaChat.Models;

namespace VocaChat.Services;

public enum AiMessageGenerationScenario
{
    UserPrivateChat,
    GroupPrimaryReply,
    GroupFollowUpReply,
    AutonomousPrivateChat,
    AutonomousPrivateChatClosing,
    AutonomousGroupChat
}

/// <summary>
/// 限定一次生成允许返回的独立聊天消息数量。
/// </summary>
public sealed record AiMessageCountRange
{
    public int Minimum { get; }
    public int Maximum { get; }

    public AiMessageCountRange(int minimum, int maximum)
    {
        if (minimum is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                "最少消息数量必须在 0 到 4 之间。");
        }

        if (maximum < minimum || maximum > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                "最多消息数量必须不小于最少数量，并且不能超过 4。");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public bool Contains(int value) =>
        value >= Minimum && value <= Maximum;
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
/// 表示当前发言账号对一个对话对象持有的、可供本轮生成参考的方向记忆。
/// </summary>
public sealed record AiConversationMemory(
    Guid OwnerAiAccountId,
    Guid SubjectAiAccountId,
    string SubjectDisplayName,
    AiMemoryType Type,
    string Summary,
    DateTime OccurredAt);

/// <summary>
/// 表示当前发言账号自己的有效个人事实，与面向其他好友的方向记忆分开保存。
/// </summary>
public sealed record AiConversationSelfMemory(
    Guid Id,
    Guid AiAccountId,
    AiSelfMemoryType Type,
    string Summary,
    AiSelfMemorySource Source,
    int Salience,
    bool IsUserLocked,
    DateTime? OccurredAt,
    DateTime UpdatedAt);

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
    public AiModelUsageCorrelation? UsageCorrelation { get; init; }
    public required AiAccount Speaker { get; init; }
    public IReadOnlyList<AiAccount> OtherParticipants { get; init; } =
        Array.Empty<AiAccount>();
    /// <summary>
    /// 当前发言者在本轮用于读取有方向关系和关系记忆的具体 AI 对象。
    /// 回应本地用户或没有可靠对象时保持为空，不能从群成员中任意猜测。
    /// </summary>
    public AiAccount? RelationshipTarget { get; init; }
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
    /// <summary>
    /// 保存业务层选出的少量方向记忆；Context Builder 仍会验证所有者和对象。
    /// </summary>
    public IReadOnlyList<AiConversationMemory> RelevantMemories { get; init; } =
        Array.Empty<AiConversationMemory>();
    /// <summary>
    /// 保存业务层为当前发言账号召回的少量个人记忆，不包含其他账号的记录。
    /// </summary>
    public IReadOnlyList<AiConversationSelfMemory> RelevantSelfMemories { get; init; } =
        Array.Empty<AiConversationSelfMemory>();
    /// <summary>
    /// 未设置时，业务层通过 ExpectedMessageCount 精确指定数量；设置后，导演可在范围内选择。
    /// </summary>
    public AiMessageCountRange? AllowedMessageCountRange { get; init; }
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
    public ConversationQuestionPolicy? QuestionPolicy { get; init; }
    /// <summary>
    /// 用户群聊中由群级导演分配给当前发言者的受众、职责与新增内容。
    /// </summary>
    public GroupConversationSpeakerPlan? GroupConversationPlan { get; init; }
}

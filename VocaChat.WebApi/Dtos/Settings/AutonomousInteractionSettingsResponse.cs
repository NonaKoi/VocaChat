namespace VocaChat.WebApi.Dtos.Settings;

/// <summary>
/// 表示通过 HTTP 返回给客户端的好友自主互动全局设置。
/// </summary>
public sealed class AutonomousInteractionSettingsResponse
{
    public bool IsEnabled { get; init; }
    public string Frequency { get; init; } = string.Empty;
    public bool AllowPrivateChats { get; init; }
    public bool AllowGroupChats { get; init; }
    public int PrivateChatContinuationRatePercent { get; init; }
    public int PrivateChatMaximumRounds { get; init; }
    public int AutonomousGroupChatMaximumMembers { get; init; }
    public int GroupChatContinuationRatePercent { get; init; }
    public int GroupChatMaximumRounds { get; init; }
    public string ReplyDelayMode { get; init; } = string.Empty;
    public long FixedReplyDelayMilliseconds { get; init; }
    public long MinimumReplyDelayMilliseconds { get; init; }
    public long MaximumReplyDelayMilliseconds { get; init; }
    public string ConsecutiveMessageDelayMode { get; init; } = string.Empty;
    public long FixedConsecutiveMessageDelayMilliseconds { get; init; }
    public long MinimumConsecutiveMessageDelayMilliseconds { get; init; }
    public long MaximumConsecutiveMessageDelayMilliseconds { get; init; }
    public int MaximumConsecutiveQuestionTurns { get; init; }
    public int MinimumReplyMessageCount { get; init; }
    public int MaximumReplyMessageCount { get; init; }
    public int GroupChatMaximumSpeakersPerTurn { get; init; }
    public int GroupChatWholeGroupMaximumSpeakersPerTurn { get; init; }
    public int GroupChatMaximumMessagesPerTurn { get; init; }
}

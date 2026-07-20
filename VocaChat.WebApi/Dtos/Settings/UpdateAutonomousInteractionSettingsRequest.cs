namespace VocaChat.WebApi.Dtos.Settings;

/// <summary>
/// 表示客户端更新好友自主互动全局设置时提交的数据。
/// </summary>
public sealed class UpdateAutonomousInteractionSettingsRequest
{
    public bool IsEnabled { get; set; }
    public string? Frequency { get; set; }
    public bool AllowPrivateChats { get; set; }
    public bool AllowGroupChats { get; set; }
    public int PrivateChatContinuationRatePercent { get; set; }
    public int PrivateChatMaximumRounds { get; set; }
    public int AutonomousGroupChatMaximumMembers { get; set; } = 6;
    public int GroupChatContinuationRatePercent { get; set; } = 80;
    public int GroupChatMaximumRounds { get; set; } = 4;
    public string? ReplyDelayMode { get; set; } = "RandomRange";
    public long FixedReplyDelayMilliseconds { get; set; } = 1200;
    public long MinimumReplyDelayMilliseconds { get; set; } = 800;
    public long MaximumReplyDelayMilliseconds { get; set; } = 1800;
    public string? ConsecutiveMessageDelayMode { get; set; } = "RandomRange";
    public long FixedConsecutiveMessageDelayMilliseconds { get; set; } = 700;
    public long MinimumConsecutiveMessageDelayMilliseconds { get; set; } = 400;
    public long MaximumConsecutiveMessageDelayMilliseconds { get; set; } = 1200;
    public int MaximumConsecutiveQuestionTurns { get; set; } = 2;
}

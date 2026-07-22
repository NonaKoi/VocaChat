namespace VocaChat.WebApi.Dtos.Settings;

/// <summary>
/// 表示客户端更新单个好友自主互动设置时提交的数据。
/// </summary>
public sealed class UpdateAiAccountAutonomySettingsRequest
{
    public bool IsEnabled { get; set; }
    public string? InitiativeLevel { get; set; }
    public bool CanInitiatePrivateChats { get; set; }
    public bool CanInitiateGroupChats { get; set; }
    public bool CanJoinGroupChats { get; set; }
    public bool UseGlobalReplyDelay { get; set; } = true;
    public string? ReplyDelayMode { get; set; } = "RandomRange";
    public long FixedReplyDelayMilliseconds { get; set; } = 1200;
    public long MinimumReplyDelayMilliseconds { get; set; } = 800;
    public long MaximumReplyDelayMilliseconds { get; set; } = 1800;
    public bool UseGlobalConsecutiveMessageDelay { get; set; } = true;
    public string? ConsecutiveMessageDelayMode { get; set; } = "RandomRange";
    public long FixedConsecutiveMessageDelayMilliseconds { get; set; } = 700;
    public long MinimumConsecutiveMessageDelayMilliseconds { get; set; } = 400;
    public long MaximumConsecutiveMessageDelayMilliseconds { get; set; } = 1200;
    public bool UseGlobalQuestionPolicy { get; set; } = true;
    public int MaximumConsecutiveQuestionTurns { get; set; } = 2;
    public bool UseGlobalReplyMessageCount { get; set; } = true;
    public int MinimumReplyMessageCount { get; set; } = 1;
    public int MaximumReplyMessageCount { get; set; } = 4;
}

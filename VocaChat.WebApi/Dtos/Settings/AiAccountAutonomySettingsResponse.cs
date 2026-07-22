using System;

namespace VocaChat.WebApi.Dtos.Settings;

/// <summary>
/// 表示通过 HTTP 返回给客户端的单个好友自主互动设置。
/// </summary>
public sealed class AiAccountAutonomySettingsResponse
{
    public Guid AiAccountId { get; init; }
    public bool IsEnabled { get; init; }
    public string InitiativeLevel { get; init; } = string.Empty;
    public bool CanInitiatePrivateChats { get; init; }
    public bool CanInitiateGroupChats { get; init; }
    public bool CanJoinGroupChats { get; init; }
    public bool UseGlobalReplyDelay { get; init; }
    public string ReplyDelayMode { get; init; } = string.Empty;
    public long FixedReplyDelayMilliseconds { get; init; }
    public long MinimumReplyDelayMilliseconds { get; init; }
    public long MaximumReplyDelayMilliseconds { get; init; }
    public bool UseGlobalConsecutiveMessageDelay { get; init; }
    public string ConsecutiveMessageDelayMode { get; init; } = string.Empty;
    public long FixedConsecutiveMessageDelayMilliseconds { get; init; }
    public long MinimumConsecutiveMessageDelayMilliseconds { get; init; }
    public long MaximumConsecutiveMessageDelayMilliseconds { get; init; }
    public bool UseGlobalQuestionPolicy { get; init; }
    public int MaximumConsecutiveQuestionTurns { get; init; }
    public bool UseGlobalReplyMessageCount { get; init; }
    public int MinimumReplyMessageCount { get; init; }
    public int MaximumReplyMessageCount { get; init; }
}

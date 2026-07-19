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
}

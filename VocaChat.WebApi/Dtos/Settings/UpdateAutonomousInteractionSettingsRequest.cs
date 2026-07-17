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
}

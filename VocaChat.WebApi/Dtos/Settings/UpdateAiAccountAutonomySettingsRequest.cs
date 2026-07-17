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
}

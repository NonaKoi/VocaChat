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
}

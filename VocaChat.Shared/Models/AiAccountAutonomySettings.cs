using System;

namespace VocaChat.Models;

/// <summary>
/// 保存一个 AI 账号参与好友自主互动时使用的专有设置。
/// </summary>
public class AiAccountAutonomySettings
{
    public Guid AiAccountId { get; private set; }
    public bool IsEnabled { get; private set; }
    public AutonomousInteractionInitiativeLevel InitiativeLevel { get; private set; }
    public bool CanInitiatePrivateChats { get; private set; }
    public bool CanInitiateGroupChats { get; private set; }
    public bool CanJoinGroupChats { get; private set; }

    /// <summary>
    /// 供 EF Core 从数据库还原设置使用。
    /// </summary>
    private AiAccountAutonomySettings()
    {
    }

    /// <summary>
    /// 为一个已有 AI 账号创建安全的默认专有设置。
    /// </summary>
    internal AiAccountAutonomySettings(Guid aiAccountId)
    {
        AiAccountId = aiAccountId;
        IsEnabled = true;
        InitiativeLevel = AutonomousInteractionInitiativeLevel.Normal;
        CanInitiatePrivateChats = true;
        CanInitiateGroupChats = true;
        CanJoinGroupChats = true;
    }

    /// <summary>
    /// 保存已经由 Service 验证通过的专有设置。
    /// </summary>
    internal void Update(
        bool isEnabled,
        AutonomousInteractionInitiativeLevel initiativeLevel,
        bool canInitiatePrivateChats,
        bool canInitiateGroupChats,
        bool canJoinGroupChats)
    {
        IsEnabled = isEnabled;
        InitiativeLevel = initiativeLevel;
        CanInitiatePrivateChats = canInitiatePrivateChats;
        CanInitiateGroupChats = canInitiateGroupChats;
        CanJoinGroupChats = canJoinGroupChats;
    }
}

using System;

namespace VocaChat.Models;

/// <summary>
/// 保存当前本地用户对好友自主互动功能的全局设置。
/// </summary>
public class AutonomousInteractionSettings
{
    internal const int SingletonId = 1;

    public int Id { get; private set; }
    public bool IsEnabled { get; private set; }
    public AutonomousInteractionFrequency Frequency { get; private set; }
    public bool AllowPrivateChats { get; private set; }
    public bool AllowGroupChats { get; private set; }

    /// <summary>
    /// 创建尚未保存的默认设置，或供 EF Core 从数据库还原设置。
    /// </summary>
    internal AutonomousInteractionSettings()
    {
        Id = SingletonId;
        IsEnabled = false;
        Frequency = AutonomousInteractionFrequency.Normal;
        AllowPrivateChats = true;
        AllowGroupChats = true;
    }

    /// <summary>
    /// 保存已经由 Service 验证通过的全局自主互动设置。
    /// </summary>
    internal void Update(
        bool isEnabled,
        AutonomousInteractionFrequency frequency,
        bool allowPrivateChats,
        bool allowGroupChats)
    {
        IsEnabled = isEnabled;
        Frequency = frequency;
        AllowPrivateChats = allowPrivateChats;
        AllowGroupChats = allowGroupChats;
    }
}

namespace VocaChat.Models;

/// <summary>
/// 表示一次好友自主私信当前所处的生命周期状态。
/// </summary>
public enum AutonomousPrivateChatSessionStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

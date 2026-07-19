namespace VocaChat.Models;

/// <summary>
/// 表示一次自主好友群聊 Session 的生命周期状态。
/// </summary>
public enum AutonomousGroupChatSessionStatus
{
    Running,
    Completed,
    Failed
}

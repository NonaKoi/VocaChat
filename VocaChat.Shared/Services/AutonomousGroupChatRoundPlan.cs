namespace VocaChat.Services;

/// <summary>
/// 表示一次自主好友群聊普通轮或收束轮的发言者与消息数量计划。
/// </summary>
public sealed class AutonomousGroupChatRoundPlan
{
    public IReadOnlyList<AutonomousGroupChatSpeakerPlan> Speakers { get; init; }
        = Array.Empty<AutonomousGroupChatSpeakerPlan>();

    public int PlannedMessageCount => Speakers.Sum(item => item.MessageCount);
}

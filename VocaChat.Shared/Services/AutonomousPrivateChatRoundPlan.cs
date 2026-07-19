using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 描述一个普通轮或收束轮中双方各自采用的消息形式和消息数量。
/// </summary>
public sealed class AutonomousPrivateChatRoundPlan
{
    public AutonomousPrivateChatMessageMode InitiatorMessageMode { get; init; }
    public AutonomousPrivateChatMessageMode RecipientMessageMode { get; init; }
    public int InitiatorMessageCount { get; init; }
    public int RecipientMessageCount { get; init; }
}

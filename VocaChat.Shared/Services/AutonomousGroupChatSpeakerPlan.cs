namespace VocaChat.Services;

/// <summary>
/// 表示一位群聊发言者在当前轮计划生成的独立消息数量。
/// </summary>
public sealed class AutonomousGroupChatSpeakerPlan
{
    public Guid SpeakerAiAccountId { get; init; }
    public int MessageCount { get; init; }
}

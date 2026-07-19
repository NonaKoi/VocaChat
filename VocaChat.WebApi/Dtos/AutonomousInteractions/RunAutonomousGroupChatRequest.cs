namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>
/// 请求受控执行一次自主好友群聊。
/// </summary>
public sealed class RunAutonomousGroupChatRequest
{
    public List<Guid> ParticipantAiAccountIds { get; init; } = new();
    public string? Topic { get; init; }
}

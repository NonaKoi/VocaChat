namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>
/// 提交一组已有好友，预览当前是否适合发起自主好友群聊。
/// </summary>
public sealed class EvaluateAutonomousGroupChatRequest
{
    public List<Guid> ParticipantAiAccountIds { get; init; } = new();
}

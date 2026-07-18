using VocaChat.WebApi.Dtos.PrivateChats;

namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>表示一次受控自主私信执行的判断、会话和消息结果。</summary>
public sealed class AutonomousPrivateChatExecutionResponse
{
    public string Status { get; init; } = string.Empty;
    public AutonomousPrivateChatDecisionResponse Decision { get; init; } = new();
    public PrivateChatResponse? PrivateChat { get; init; }
    public bool PrivateChatCreated { get; init; }
    public PrivateMessageResponse? InitiatorMessage { get; init; }
    public PrivateMessageResponse? RecipientReply { get; init; }
    public string? ErrorMessage { get; init; }
}

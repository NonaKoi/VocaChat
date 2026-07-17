namespace VocaChat.WebApi.Dtos.PrivateChats;

/// <summary>表示一次完整私聊交互保存的用户消息和模拟回复。</summary>
public sealed class SendPrivateMessageResponse
{
    public PrivateMessageResponse UserMessage { get; init; } = new();
    public PrivateMessageResponse AiReply { get; init; } = new();
}

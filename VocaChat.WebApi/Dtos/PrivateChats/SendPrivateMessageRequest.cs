namespace VocaChat.WebApi.Dtos.PrivateChats;

/// <summary>发送一条私聊用户消息所需的数据。</summary>
public sealed class SendPrivateMessageRequest
{
    public string Content { get; init; } = string.Empty;
}

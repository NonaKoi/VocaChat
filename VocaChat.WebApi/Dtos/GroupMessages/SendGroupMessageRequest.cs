namespace VocaChat.WebApi.Dtos.GroupMessages;

/// <summary>
/// 表示客户端在群聊中发送的用户消息内容。
/// </summary>
public sealed class SendGroupMessageRequest
{
    public string? Content { get; set; }
}

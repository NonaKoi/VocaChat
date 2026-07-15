namespace VocaChat.WebApi.Dtos.GroupMessages;

/// <summary>
/// 表示一次成功群聊交互保存的用户消息和模拟 AI 回复。
/// </summary>
public sealed class SendGroupMessageResponse
{
    public GroupMessageResponse UserMessage { get; init; } = new();
    public GroupMessageResponse AiReply { get; init; } = new();
}

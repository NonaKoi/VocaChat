namespace VocaChat.WebApi.Dtos.PrivateChats;

/// <summary>表示一次完整私聊交互保存的用户消息和一至多条 AI 回复。</summary>
public sealed class SendPrivateMessageResponse
{
    public PrivateMessageResponse UserMessage { get; init; } = new();
    public List<PrivateMessageResponse> AiReplies { get; init; } = new();
    public string ReplyCompletion { get; init; } = "Complete";
    public string? WarningMessage { get; init; }
}

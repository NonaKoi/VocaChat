namespace VocaChat.WebApi.Dtos.GroupMessages;

/// <summary>
/// 表示一次群聊交互保存的用户消息、一至多条 AI 回复和完成状态。
/// </summary>
public sealed class SendGroupMessageResponse
{
    public GroupMessageResponse UserMessage { get; init; } = new();
    public IReadOnlyList<GroupMessageResponse> AiReplies { get; init; } =
        Array.Empty<GroupMessageResponse>();
    public string ReplyCompletion { get; init; } = "Complete";
    public string? WarningMessage { get; init; }
}

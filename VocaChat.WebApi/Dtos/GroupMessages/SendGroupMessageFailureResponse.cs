namespace VocaChat.WebApi.Dtos.GroupMessages;

/// <summary>
/// 表示群聊交互失败，并明确返回失败前已经保存的消息。
/// </summary>
public sealed class SendGroupMessageFailureResponse
{
    public string Message { get; init; } = string.Empty;
    public GroupMessageResponse? SavedUserMessage { get; init; }
    public IReadOnlyList<GroupMessageResponse> SavedAiReplies { get; init; } =
        Array.Empty<GroupMessageResponse>();
}

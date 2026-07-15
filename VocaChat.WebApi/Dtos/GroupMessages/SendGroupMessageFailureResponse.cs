namespace VocaChat.WebApi.Dtos.GroupMessages;

/// <summary>
/// 表示群聊交互失败，并在部分失败时明确返回已经保存的用户消息。
/// </summary>
public sealed class SendGroupMessageFailureResponse
{
    public string Message { get; init; } = string.Empty;
    public GroupMessageResponse? SavedUserMessage { get; init; }
}

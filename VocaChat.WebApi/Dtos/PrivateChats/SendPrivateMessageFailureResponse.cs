namespace VocaChat.WebApi.Dtos.PrivateChats;

/// <summary>表示私聊发送失败或部分成功。</summary>
public sealed class SendPrivateMessageFailureResponse
{
    public string Message { get; init; } = string.Empty;
    public PrivateMessageResponse? SavedUserMessage { get; init; }
}

using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 组织一次完整的私聊用户消息和模拟 AI 回复流程。
/// </summary>
public sealed class PrivateChatInteractionService
{
    private readonly PrivateChatService _privateChatService;
    private readonly FakeAiReplyService _fakeAiReplyService;

    public PrivateChatInteractionService(
        PrivateChatService privateChatService,
        FakeAiReplyService fakeAiReplyService)
    {
        _privateChatService = privateChatService;
        _fakeAiReplyService = fakeAiReplyService;
    }

    public PrivateChatInteractionResult ProcessUserMessage(
        PrivateChat privateChat,
        string content)
    {
        if (!_privateChatService.TrySaveUserMessage(
                privateChat,
                content,
                out PrivateMessage? userMessage,
                out string userMessageError))
        {
            return PrivateChatInteractionResult.UserMessageRejected(
                userMessageError);
        }

        AiAccount aiAccount = privateChat.Contact.AiAccount;
        string replyContent = _fakeAiReplyService.GenerateReply(
            aiAccount,
            userMessage!.Content);

        if (!_privateChatService.TrySaveAiReply(
                privateChat,
                aiAccount,
                replyContent,
                out PrivateMessage? aiReply,
                out string aiReplyError))
        {
            return PrivateChatInteractionResult.AiReplyFailed(
                userMessage,
                aiReplyError);
        }

        return PrivateChatInteractionResult.Succeeded(userMessage, aiReply!);
    }
}

public enum PrivateChatInteractionStatus
{
    Succeeded,
    UserMessageRejected,
    AiReplyFailed
}

public sealed class PrivateChatInteractionResult
{
    public PrivateChatInteractionStatus Status { get; }
    public PrivateMessage? UserMessage { get; }
    public PrivateMessage? AiReply { get; }
    public string ErrorMessage { get; }

    private PrivateChatInteractionResult(
        PrivateChatInteractionStatus status,
        PrivateMessage? userMessage,
        PrivateMessage? aiReply,
        string errorMessage)
    {
        Status = status;
        UserMessage = userMessage;
        AiReply = aiReply;
        ErrorMessage = errorMessage;
    }

    public static PrivateChatInteractionResult Succeeded(
        PrivateMessage userMessage,
        PrivateMessage aiReply) =>
        new(
            PrivateChatInteractionStatus.Succeeded,
            userMessage,
            aiReply,
            string.Empty);

    public static PrivateChatInteractionResult UserMessageRejected(
        string errorMessage) =>
        new(
            PrivateChatInteractionStatus.UserMessageRejected,
            null,
            null,
            errorMessage);

    public static PrivateChatInteractionResult AiReplyFailed(
        PrivateMessage userMessage,
        string errorMessage) =>
        new(
            PrivateChatInteractionStatus.AiReplyFailed,
            userMessage,
            null,
            errorMessage);
}

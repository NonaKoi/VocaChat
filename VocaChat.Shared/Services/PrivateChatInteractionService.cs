using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 组织一次完整的私聊用户消息和模拟 AI 回复流程。
/// </summary>
public sealed class PrivateChatInteractionService
{
    private readonly PrivateChatService _privateChatService;
    private readonly IAiMessageGenerator _messageGenerator;
    private readonly IConversationDirector _conversationDirector;

    public PrivateChatInteractionService(
        PrivateChatService privateChatService,
        IAiMessageGenerator messageGenerator,
        IConversationDirector conversationDirector)
    {
        _privateChatService = privateChatService;
        _messageGenerator = messageGenerator;
        _conversationDirector = conversationDirector;
    }

    public async Task<PrivateChatInteractionResult> ProcessUserMessageAsync(
        PrivateChat privateChat,
        string content,
        CancellationToken cancellationToken = default)
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

        AiAccount aiAccount = privateChat.Contact!.AiAccount;
        string replyContent;

        try
        {
            AiDialogueMessage replyTarget = new(
                userMessage!.SenderDisplayName,
                userMessage.Content,
                userMessage.SenderType,
                userMessage.SenderAiAccountId,
                userMessage.Id,
                userMessage.SentAt);
            AiMessageGenerationRequest generationRequest = new()
            {
                Scenario = AiMessageGenerationScenario.UserPrivateChat,
                Speaker = aiAccount,
                FocusContent = replyTarget.Content,
                ReplyTarget = AiDialogueReplyTarget.ReplyTo(replyTarget),
                ConversationAnchor = replyTarget,
                RecentMessages = _privateChatService
                    .GetOrderedChatHistory(privateChat.Id)
                    .TakeLast(12)
                    .Select(message => new AiDialogueMessage(
                        message.SenderDisplayName,
                        message.Content,
                        message.SenderType,
                        message.SenderAiAccountId,
                        message.Id,
                        message.SentAt))
                    .ToList()
                    .AsReadOnly(),
                ExpectedMessageCount = 1
            };
            ConversationDirectionPlan directionPlan =
                await _conversationDirector.CreatePlanAsync(
                    generationRequest,
                    cancellationToken);
            generationRequest = generationRequest with
            {
                DirectionPlan = directionPlan,
                ActionPlan = directionPlan.ActionPlan
            };
            IReadOnlyList<string> generatedMessages =
                await _messageGenerator.GenerateMessagesAsync(
                    generationRequest,
                    cancellationToken);
            replyContent = generatedMessages.Single();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return PrivateChatInteractionResult.AiReplyFailed(
                userMessage!,
                exception.Message);
        }

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

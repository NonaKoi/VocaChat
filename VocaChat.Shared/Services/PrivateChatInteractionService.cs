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
        IReadOnlyList<string> replyContents;

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
                AllowedMessageCountRange = new AiMessageCountRange(1, 3),
                ExpectedMessageCount = 1
            };
            ConversationDirectionPlan directionPlan =
                await _conversationDirector.CreatePlanAsync(
                    generationRequest,
                    cancellationToken);
            generationRequest = generationRequest with
            {
                DirectionPlan = directionPlan,
                ActionPlan = directionPlan.ActionPlan,
                ExpectedMessageCount = directionPlan.SelectedMessageCount
            };
            replyContents =
                await _messageGenerator.GenerateMessagesAsync(
                    generationRequest,
                    cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return PrivateChatInteractionResult.AiReplyFailed(
                userMessage!,
                exception.Message);
        }

        if (!_privateChatService.TrySaveAiReplies(
                privateChat,
                aiAccount,
                replyContents,
                out IReadOnlyList<PrivateMessage> aiReplies,
                out string aiReplyError))
        {
            return PrivateChatInteractionResult.AiReplyFailed(
                userMessage,
                aiReplyError);
        }

        return PrivateChatInteractionResult.Succeeded(userMessage, aiReplies);
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
    public IReadOnlyList<PrivateMessage> AiReplies { get; }
    public string ErrorMessage { get; }

    private PrivateChatInteractionResult(
        PrivateChatInteractionStatus status,
        PrivateMessage? userMessage,
        IReadOnlyList<PrivateMessage>? aiReplies,
        string errorMessage)
    {
        Status = status;
        UserMessage = userMessage;
        AiReplies = aiReplies ?? Array.Empty<PrivateMessage>();
        ErrorMessage = errorMessage;
    }

    public static PrivateChatInteractionResult Succeeded(
        PrivateMessage userMessage,
        IReadOnlyList<PrivateMessage> aiReplies) =>
        new(
            PrivateChatInteractionStatus.Succeeded,
            userMessage,
            aiReplies,
            string.Empty);

    public static PrivateChatInteractionResult UserMessageRejected(
        string errorMessage) =>
        new(
            PrivateChatInteractionStatus.UserMessageRejected,
            null,
            Array.Empty<PrivateMessage>(),
            errorMessage);

    public static PrivateChatInteractionResult AiReplyFailed(
        PrivateMessage userMessage,
        string errorMessage) =>
        new(
            PrivateChatInteractionStatus.AiReplyFailed,
            userMessage,
            Array.Empty<PrivateMessage>(),
            errorMessage);
}

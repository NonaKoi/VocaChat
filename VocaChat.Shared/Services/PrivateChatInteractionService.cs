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
    private readonly AiReplyTimingScheduler _replyTimingScheduler;
    private readonly AiReplyMessageCountSettingsResolver
        _replyMessageCountSettingsResolver;
    private readonly ConversationQuestionPolicyService _questionPolicyService;
    private readonly AiIdentityContinuityService _identityContinuityService;
    private readonly AiWorldKnowledgeMessageProcessor?
        _worldKnowledgeProcessor;
    private readonly AiWorldConversationContextService?
        _worldConversationContextService;

    public PrivateChatInteractionService(
        PrivateChatService privateChatService,
        IAiMessageGenerator messageGenerator,
        IConversationDirector conversationDirector,
        AiReplyTimingScheduler replyTimingScheduler,
        AiReplyMessageCountSettingsResolver replyMessageCountSettingsResolver,
        ConversationQuestionPolicyService questionPolicyService,
        AiIdentityContinuityService identityContinuityService,
        AiWorldKnowledgeMessageProcessor? worldKnowledgeProcessor = null,
        AiWorldConversationContextService?
            worldConversationContextService = null)
    {
        _privateChatService = privateChatService;
        _messageGenerator = messageGenerator;
        _conversationDirector = conversationDirector;
        _replyTimingScheduler = replyTimingScheduler;
        _replyMessageCountSettingsResolver = replyMessageCountSettingsResolver
            ?? throw new ArgumentNullException(
                nameof(replyMessageCountSettingsResolver));
        _questionPolicyService = questionPolicyService
            ?? throw new ArgumentNullException(nameof(questionPolicyService));
        _identityContinuityService = identityContinuityService
            ?? throw new ArgumentNullException(nameof(identityContinuityService));
        _worldKnowledgeProcessor = worldKnowledgeProcessor;
        _worldConversationContextService =
            worldConversationContextService;
    }

    public async Task<PrivateChatInteractionResult> ProcessUserMessageAsync(
        PrivateChat privateChat,
        string content,
        CancellationToken cancellationToken = default)
    {
        return await ProcessUserMessageAsync(
            privateChat,
            content,
            null,
            cancellationToken);
    }

    /// <summary>
    /// 使用客户端预先生成的消息 Id 执行私聊交互，便于界面立即展示待发送消息。
    /// </summary>
    public async Task<PrivateChatInteractionResult> ProcessUserMessageAsync(
        PrivateChat privateChat,
        string content,
        Guid? userMessageId,
        CancellationToken cancellationToken = default)
    {
        if (!_privateChatService.TrySaveUserMessage(
                privateChat,
                content,
                userMessageId,
                out PrivateMessage? userMessage,
                out string userMessageError))
        {
            return PrivateChatInteractionResult.UserMessageRejected(
                userMessageError);
        }

        AiAccount aiAccount = privateChat.Contact!.AiAccount;
        Guid aiResponseBatchId = Guid.NewGuid();
        if (_worldKnowledgeProcessor is not null)
        {
            await _worldKnowledgeProcessor.ProcessPrivateMessageAsync(
                userMessage!.Id,
                new AiModelUsageCorrelation
                {
                    PrivateChatId = privateChat.Id,
                    AiResponseBatchId = aiResponseBatchId
                },
                cancellationToken);
        }

        IReadOnlyList<string> replyContents;
        AiMessageGenerationRequest? completedRequest = null;
        AiIdentityContinuityPlan? continuityPlan = null;

        try
        {
            AiDialogueMessage replyTarget = new(
                userMessage!.SenderDisplayName,
                userMessage.Content,
                userMessage.SenderType,
                userMessage.SenderAiAccountId,
                userMessage.Id,
                userMessage.SentAt);
            IReadOnlyList<AiDialogueMessage> recentMessages =
                _privateChatService
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
                    .AsReadOnly();
            AiMessageGenerationRequest generationRequest = new()
            {
                Scenario = AiMessageGenerationScenario.UserPrivateChat,
                UsageCorrelation = new AiModelUsageCorrelation
                {
                    PrivateChatId = privateChat.Id,
                    AiResponseBatchId = aiResponseBatchId
                },
                Speaker = aiAccount,
                FocusContent = replyTarget.Content,
                ReplyTarget = AiDialogueReplyTarget.ReplyTo(replyTarget),
                ConversationAnchor = replyTarget,
                RecentMessages = recentMessages,
                QuestionPolicy = _questionPolicyService.CreatePolicy(
                    aiAccount.Id,
                    recentMessages),
                AllowedMessageCountRange =
                    _replyMessageCountSettingsResolver.Resolve(aiAccount.Id),
                ExpectedMessageCount = 1
            };
            generationRequest = _identityContinuityService
                .PrepareGenerationRequest(generationRequest);
            generationRequest = _worldConversationContextService?
                .PrepareGenerationRequest(generationRequest)
                ?? generationRequest;
            ConversationDirectionPlan directionPlan =
                await _conversationDirector.CreatePlanAsync(
                    generationRequest,
                    cancellationToken);
            continuityPlan = _identityContinuityService
                .ValidateDirectionPlan(generationRequest, directionPlan);
            directionPlan = continuityPlan.DirectionPlan;
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
            completedRequest = generationRequest;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return PrivateChatInteractionResult.AiReplyFailed(
                userMessage!,
                exception.Message);
        }

        List<PrivateMessage> savedAiReplies = new();

        for (int replyIndex = 0; replyIndex < replyContents.Count; replyIndex++)
        {
            string replyContent = replyContents[replyIndex];
            PrivateMessage previousMessage =
                savedAiReplies.LastOrDefault() ?? userMessage!;
            try
            {
                if (replyIndex == 0)
                {
                    await _replyTimingScheduler.WaitForReplyAsync(
                        aiAccount.Id,
                        previousMessage.SentAt,
                        cancellationToken);
                }
                else
                {
                    await _replyTimingScheduler.WaitForConsecutiveMessageAsync(
                        aiAccount.Id,
                        previousMessage.SentAt,
                        cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await ApplyIdentityContinuityAsync(
                    privateChat.Id,
                    completedRequest,
                    continuityPlan,
                    savedAiReplies,
                    cancellationToken);
                return savedAiReplies.Count == 0
                    ? PrivateChatInteractionResult.AiReplyFailed(
                        userMessage!,
                        savedAiReplies.AsReadOnly(),
                        exception.Message)
                    : PrivateChatInteractionResult.PartiallySucceeded(
                        userMessage!,
                        savedAiReplies.AsReadOnly(),
                        exception.Message);
            }

            if (!_privateChatService.TrySaveAiResponseMessage(
                    privateChat,
                    aiAccount,
                    replyContent,
                    aiResponseBatchId,
                    out PrivateMessage? aiReply,
                    out string aiReplyError)
                || aiReply is null)
            {
                await ApplyIdentityContinuityAsync(
                    privateChat.Id,
                    completedRequest,
                    continuityPlan,
                    savedAiReplies,
                    cancellationToken);
                return savedAiReplies.Count == 0
                    ? PrivateChatInteractionResult.AiReplyFailed(
                        userMessage!,
                        savedAiReplies.AsReadOnly(),
                        aiReplyError)
                    : PrivateChatInteractionResult.PartiallySucceeded(
                        userMessage!,
                        savedAiReplies.AsReadOnly(),
                        aiReplyError);
            }

            savedAiReplies.Add(aiReply);
            if (_worldKnowledgeProcessor is not null)
            {
                await _worldKnowledgeProcessor.ProcessPrivateMessageAsync(
                    aiReply.Id,
                    cancellationToken);
            }
        }

        await ApplyIdentityContinuityAsync(
            privateChat.Id,
            completedRequest,
            continuityPlan,
            savedAiReplies,
            cancellationToken);

        return PrivateChatInteractionResult.Succeeded(
            userMessage!,
            savedAiReplies.AsReadOnly());
    }

    private async Task ApplyIdentityContinuityAsync(
        Guid privateChatId,
        AiMessageGenerationRequest? request,
        AiIdentityContinuityPlan? continuityPlan,
        IReadOnlyList<PrivateMessage> messages,
        CancellationToken cancellationToken)
    {
        if (request is null || continuityPlan is null || messages.Count == 0)
        {
            return;
        }

        await _identityContinuityService.ApplyAfterMessagesSavedAsync(
            request,
            continuityPlan,
            privateChatId,
            messages.Select(message => new AiPersistedMessageEvidence(
                    message.Id,
                    message.Content,
                    message.SentAt))
                .ToList()
                .AsReadOnly(),
            cancellationToken);
    }
}

public enum PrivateChatInteractionStatus
{
    Succeeded,
    PartiallySucceeded,
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
        IReadOnlyList<PrivateMessage> savedAiReplies,
        string errorMessage) =>
        new(
            PrivateChatInteractionStatus.AiReplyFailed,
            userMessage,
            savedAiReplies,
            errorMessage);

    public static PrivateChatInteractionResult AiReplyFailed(
        PrivateMessage userMessage,
        string errorMessage) =>
        AiReplyFailed(
            userMessage,
            Array.Empty<PrivateMessage>(),
            errorMessage);

    public static PrivateChatInteractionResult PartiallySucceeded(
        PrivateMessage userMessage,
        IReadOnlyList<PrivateMessage> savedAiReplies,
        string errorMessage) =>
        new(
            PrivateChatInteractionStatus.PartiallySucceeded,
            userMessage,
            savedAiReplies,
            errorMessage);
}

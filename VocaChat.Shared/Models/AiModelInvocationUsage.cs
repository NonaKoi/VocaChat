namespace VocaChat.Models;

/// <summary>
/// 保存一次真实模型请求返回的 Token 用量，不保存提示词、回复正文或密钥。
/// </summary>
public sealed class AiModelInvocationUsage
{
    internal const int ModelNameMaxLength = 200;

    public Guid Id { get; private set; }
    public AiModelInvocationStage Stage { get; private set; }
    public string ModelName { get; private set; }
    public Guid? AiAccountId { get; private set; }
    public Guid? GroupChatId { get; private set; }
    public Guid? PrivateChatId { get; private set; }
    public Guid? AutonomousPrivateChatSessionId { get; private set; }
    public Guid? AutonomousGroupChatSessionId { get; private set; }
    public Guid? InteractionBatchId { get; private set; }
    public Guid? AiResponseBatchId { get; private set; }
    public int AttemptNumber { get; private set; }
    public int? PromptTokens { get; private set; }
    public int? CompletionTokens { get; private set; }
    public int? TotalTokens { get; private set; }
    public int? PromptCacheHitTokens { get; private set; }
    public int? PromptCacheMissTokens { get; private set; }
    public int? ReasoningTokens { get; private set; }
    public bool UsageReported { get; private set; }
    public DateTime RecordedAt { get; private set; }

    private AiModelInvocationUsage()
    {
        ModelName = string.Empty;
    }

    internal AiModelInvocationUsage(
        AiModelInvocationStage stage,
        string modelName,
        Guid? aiAccountId,
        Guid? groupChatId,
        Guid? privateChatId,
        Guid? autonomousPrivateChatSessionId,
        Guid? autonomousGroupChatSessionId,
        Guid? interactionBatchId,
        Guid? aiResponseBatchId,
        int attemptNumber,
        int? promptTokens,
        int? completionTokens,
        int? totalTokens,
        int? promptCacheHitTokens,
        int? promptCacheMissTokens,
        int? reasoningTokens,
        bool usageReported,
        DateTime recordedAt)
    {
        Id = Guid.NewGuid();
        Stage = stage;
        ModelName = modelName;
        AiAccountId = aiAccountId;
        GroupChatId = groupChatId;
        PrivateChatId = privateChatId;
        AutonomousPrivateChatSessionId = autonomousPrivateChatSessionId;
        AutonomousGroupChatSessionId = autonomousGroupChatSessionId;
        InteractionBatchId = interactionBatchId;
        AiResponseBatchId = aiResponseBatchId;
        AttemptNumber = attemptNumber;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens;
        PromptCacheHitTokens = promptCacheHitTokens;
        PromptCacheMissTokens = promptCacheMissTokens;
        ReasoningTokens = reasoningTokens;
        UsageReported = usageReported;
        RecordedAt = recordedAt;
    }
}

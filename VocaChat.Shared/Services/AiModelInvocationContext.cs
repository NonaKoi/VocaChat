using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 为一次模型请求提供可持久化的业务关联，不携带提示词或其他敏感内容。
/// </summary>
public sealed record AiModelInvocationContext
{
    public required AiModelInvocationStage Stage { get; init; }
    public int AttemptNumber { get; init; } = 1;
    public Guid? AiAccountId { get; init; }
    public Guid? GroupChatId { get; init; }
    public Guid? PrivateChatId { get; init; }
    public Guid? AutonomousPrivateChatSessionId { get; init; }
    public Guid? AutonomousGroupChatSessionId { get; init; }
    public Guid? InteractionBatchId { get; init; }
    public Guid? AiResponseBatchId { get; init; }
}

/// <summary>
/// 在导演和生成器之间传递同一次聊天操作的非敏感关联标识。
/// </summary>
public sealed record AiModelUsageCorrelation
{
    public Guid? GroupChatId { get; init; }
    public Guid? PrivateChatId { get; init; }
    public Guid? AutonomousPrivateChatSessionId { get; init; }
    public Guid? AutonomousGroupChatSessionId { get; init; }
    public Guid? InteractionBatchId { get; init; }
    public Guid? AiResponseBatchId { get; init; }

    public AiModelInvocationContext CreateInvocationContext(
        AiModelInvocationStage stage,
        int attemptNumber,
        Guid? aiAccountId = null) =>
        new()
        {
            Stage = stage,
            AttemptNumber = attemptNumber,
            AiAccountId = aiAccountId,
            GroupChatId = GroupChatId,
            PrivateChatId = PrivateChatId,
            AutonomousPrivateChatSessionId =
                AutonomousPrivateChatSessionId,
            AutonomousGroupChatSessionId =
                AutonomousGroupChatSessionId,
            InteractionBatchId = InteractionBatchId,
            AiResponseBatchId = AiResponseBatchId
        };
}

/// <summary>
/// 表示模型供应商在一次响应中返回的可选 Token 统计。
/// </summary>
public sealed record AiModelTokenUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    int? PromptCacheHitTokens,
    int? PromptCacheMissTokens,
    int? ReasoningTokens);

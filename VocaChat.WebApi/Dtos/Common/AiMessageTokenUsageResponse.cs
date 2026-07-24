namespace VocaChat.WebApi.Dtos.Common;

/// <summary>
/// 表示一个模型调用阶段在一次或多次尝试中的聚合 Token 用量。
/// </summary>
public sealed class AiModelStageTokenUsageResponse
{
    public bool UsageComplete { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public int? TotalTokens { get; init; }
    public int? CacheHitTokens { get; init; }
    public int? CacheMissTokens { get; init; }
    public int? ReasoningTokens { get; init; }
    public int AttemptCount { get; init; }
}

/// <summary>
/// 表示一条 AI 消息关联的导演、回复生成和记忆判断用量。
/// </summary>
public sealed class AiMessageTokenUsageResponse
{
    public AiModelStageTokenUsageResponse? GroupDirector { get; init; }
    public AiModelStageTokenUsageResponse? ConversationDirector { get; init; }
    public AiModelStageTokenUsageResponse? ReplyGeneration { get; init; }
    public AiModelStageTokenUsageResponse? SelfMemoryJudgment { get; init; }
    public AiModelStageTokenUsageResponse? WorldKnowledgeExtraction
    {
        get;
        init;
    }
    public bool UsageComplete { get; init; }
    public int TotalTokens { get; init; }
    public int InteractionSharedMessageCount { get; init; }
    public int ResponseSharedMessageCount { get; init; }
}

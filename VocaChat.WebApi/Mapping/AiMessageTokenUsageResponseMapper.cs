using VocaChat.Services;
using VocaChat.WebApi.Dtos.Common;

namespace VocaChat.WebApi.Mapping;

internal static class AiMessageTokenUsageResponseMapper
{
    public static AiMessageTokenUsageResponse? ToResponse(
        AiMessageTokenUsageSummary? summary)
    {
        return summary is null
            ? null
            : new AiMessageTokenUsageResponse
            {
                GroupDirector = ToStageResponse(summary.GroupDirector),
                ConversationDirector = ToStageResponse(
                    summary.ConversationDirector),
                ReplyGeneration = ToStageResponse(summary.ReplyGeneration),
                UsageComplete = summary.UsageComplete,
                TotalTokens = summary.TotalTokens,
                InteractionSharedMessageCount =
                    summary.InteractionSharedMessageCount,
                ResponseSharedMessageCount =
                    summary.ResponseSharedMessageCount
            };
    }

    private static AiModelStageTokenUsageResponse? ToStageResponse(
        AiModelStageTokenUsageSummary? summary)
    {
        return summary is null
            ? null
            : new AiModelStageTokenUsageResponse
            {
                UsageComplete = summary.UsageComplete,
                InputTokens = summary.PromptTokens,
                OutputTokens = summary.CompletionTokens,
                TotalTokens = summary.TotalTokens,
                CacheHitTokens = summary.PromptCacheHitTokens,
                CacheMissTokens = summary.PromptCacheMissTokens,
                ReasoningTokens = summary.ReasoningTokens,
                AttemptCount = summary.AttemptCount
            };
    }
}

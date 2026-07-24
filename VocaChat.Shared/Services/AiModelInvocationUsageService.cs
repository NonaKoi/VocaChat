using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 保存真实模型调用用量，并按消息批次聚合气泡展示所需的数据。
/// </summary>
public sealed class AiModelInvocationUsageService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiModelInvocationUsageService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 尽力保存一次调用记录。用量记录故障不能中断已经成功的聊天生成。
    /// </summary>
    public bool TryRecord(
        AiModelInvocationContext context,
        string modelName,
        AiModelTokenUsage? usage)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.AttemptNumber <= 0)
        {
            return false;
        }

        string normalizedModelName = modelName?.Trim() ?? string.Empty;
        if (normalizedModelName.Length == 0)
        {
            normalizedModelName = "unknown";
        }

        if (normalizedModelName.Length >
            AiModelInvocationUsage.ModelNameMaxLength)
        {
            normalizedModelName = normalizedModelName[
                ..AiModelInvocationUsage.ModelNameMaxLength];
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            dbContext.AiModelInvocationUsages.Add(
                new AiModelInvocationUsage(
                    context.Stage,
                    normalizedModelName,
                    context.AiAccountId,
                    context.GroupChatId,
                    context.PrivateChatId,
                    context.AutonomousPrivateChatSessionId,
                    context.AutonomousGroupChatSessionId,
                    context.InteractionBatchId,
                    context.AiResponseBatchId,
                    context.AttemptNumber,
                    usage?.PromptTokens,
                    usage?.CompletionTokens,
                    usage?.TotalTokens,
                    usage?.PromptCacheHitTokens,
                    usage?.PromptCacheMissTokens,
                    usage?.ReasoningTokens,
                    usage is not null,
                    DateTime.UtcNow));
            dbContext.SaveChanges();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 一次性读取群消息需要的共享群级导演和回复批次用量。
    /// </summary>
    public IReadOnlyDictionary<Guid, AiMessageTokenUsageSummary>
        GetForGroupMessages(IReadOnlyList<GroupMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        List<GroupMessage> aiMessages = messages
            .Where(message => message.SenderType ==
                MessageSenderType.AiAccount)
            .ToList();
        HashSet<Guid> interactionBatchIds = aiMessages
            .Select(message => message.InteractionBatchId)
            .OfType<Guid>()
            .ToHashSet();
        HashSet<Guid> responseBatchIds = aiMessages
            .Select(message => message.AiResponseBatchId)
            .OfType<Guid>()
            .ToHashSet();

        if (interactionBatchIds.Count == 0 && responseBatchIds.Count == 0)
        {
            return new Dictionary<Guid, AiMessageTokenUsageSummary>();
        }

        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();
        List<AiModelInvocationUsage> usages = dbContext
            .AiModelInvocationUsages
            .AsNoTracking()
            .Where(usage =>
                (usage.InteractionBatchId.HasValue
                    && interactionBatchIds.Contains(
                        usage.InteractionBatchId.Value))
                || (usage.AiResponseBatchId.HasValue
                    && responseBatchIds.Contains(
                        usage.AiResponseBatchId.Value)))
            .ToList();

        return aiMessages
            .Select(message => new
            {
                message.Id,
                Summary = CreateSummary(
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.GroupDirector
                        && usage.InteractionBatchId ==
                            message.InteractionBatchId),
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.ConversationDirector
                        && usage.AiResponseBatchId ==
                            message.AiResponseBatchId),
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.ReplyGeneration
                        && usage.AiResponseBatchId ==
                            message.AiResponseBatchId),
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.SelfMemoryJudgment
                        && usage.AiResponseBatchId ==
                            message.AiResponseBatchId),
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.WorldKnowledgeExtraction
                        && (usage.AiResponseBatchId.HasValue
                            ? usage.AiResponseBatchId ==
                                message.AiResponseBatchId
                            : usage.InteractionBatchId ==
                                message.InteractionBatchId)),
                    interactionSharedMessageCount: aiMessages.Count(item =>
                        item.InteractionBatchId is not null
                        && item.InteractionBatchId ==
                            message.InteractionBatchId),
                    responseSharedMessageCount: aiMessages.Count(item =>
                        item.AiResponseBatchId is not null
                        && item.AiResponseBatchId ==
                            message.AiResponseBatchId))
            })
            .Where(item => item.Summary is not null)
            .ToDictionary(
                item => item.Id,
                item => item.Summary!);
    }

    /// <summary>
    /// 一次性读取私聊消息中单人导演和回复生成批次的用量。
    /// </summary>
    public IReadOnlyDictionary<Guid, AiMessageTokenUsageSummary>
        GetForPrivateMessages(IReadOnlyList<PrivateMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        List<PrivateMessage> aiMessages = messages
            .Where(message => message.SenderType ==
                MessageSenderType.AiAccount)
            .ToList();
        HashSet<Guid> responseBatchIds = aiMessages
            .Select(message => message.AiResponseBatchId)
            .OfType<Guid>()
            .ToHashSet();

        if (responseBatchIds.Count == 0)
        {
            return new Dictionary<Guid, AiMessageTokenUsageSummary>();
        }

        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();
        List<AiModelInvocationUsage> usages = dbContext
            .AiModelInvocationUsages
            .AsNoTracking()
            .Where(usage =>
                usage.AiResponseBatchId.HasValue
                && responseBatchIds.Contains(
                    usage.AiResponseBatchId.Value))
            .ToList();

        return aiMessages
            .Select(message => new
            {
                message.Id,
                Summary = CreateSummary(
                    Array.Empty<AiModelInvocationUsage>(),
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.ConversationDirector
                        && usage.AiResponseBatchId ==
                            message.AiResponseBatchId),
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.ReplyGeneration
                        && usage.AiResponseBatchId ==
                            message.AiResponseBatchId),
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.SelfMemoryJudgment
                        && usage.AiResponseBatchId ==
                            message.AiResponseBatchId),
                    usages.Where(usage =>
                        usage.Stage ==
                            AiModelInvocationStage.WorldKnowledgeExtraction
                        && usage.AiResponseBatchId ==
                            message.AiResponseBatchId),
                    interactionSharedMessageCount: 0,
                    responseSharedMessageCount: aiMessages.Count(item =>
                        item.AiResponseBatchId is not null
                        && item.AiResponseBatchId ==
                            message.AiResponseBatchId))
            })
            .Where(item => item.Summary is not null)
            .ToDictionary(
                item => item.Id,
                item => item.Summary!);
    }

    private static AiMessageTokenUsageSummary? CreateSummary(
        IEnumerable<AiModelInvocationUsage> groupDirectorUsages,
        IEnumerable<AiModelInvocationUsage> conversationDirectorUsages,
        IEnumerable<AiModelInvocationUsage> replyGenerationUsages,
        IEnumerable<AiModelInvocationUsage> selfMemoryJudgmentUsages,
        IEnumerable<AiModelInvocationUsage> worldKnowledgeExtractionUsages,
        int interactionSharedMessageCount,
        int responseSharedMessageCount)
    {
        AiModelStageTokenUsageSummary? groupDirector =
            AggregateStage(groupDirectorUsages);
        AiModelStageTokenUsageSummary? conversationDirector =
            AggregateStage(conversationDirectorUsages);
        AiModelStageTokenUsageSummary? replyGeneration =
            AggregateStage(replyGenerationUsages);
        AiModelStageTokenUsageSummary? selfMemoryJudgment =
            AggregateStage(selfMemoryJudgmentUsages);
        AiModelStageTokenUsageSummary? worldKnowledgeExtraction =
            AggregateStage(worldKnowledgeExtractionUsages);
        AiModelStageTokenUsageSummary[] stages =
            new[]
            {
                groupDirector,
                conversationDirector,
                replyGeneration,
                selfMemoryJudgment,
                worldKnowledgeExtraction
            }
            .OfType<AiModelStageTokenUsageSummary>()
            .ToArray();

        if (stages.Length == 0)
        {
            return null;
        }

        return new AiMessageTokenUsageSummary(
            groupDirector,
            conversationDirector,
            replyGeneration,
            selfMemoryJudgment,
            worldKnowledgeExtraction,
            stages.All(stage => stage.UsageComplete),
            stages.Where(stage => stage.TotalTokens.HasValue)
                .Sum(stage => stage.TotalTokens!.Value),
            interactionSharedMessageCount,
            responseSharedMessageCount);
    }

    private static AiModelStageTokenUsageSummary? AggregateStage(
        IEnumerable<AiModelInvocationUsage> source)
    {
        List<AiModelInvocationUsage> usages = source.ToList();
        if (usages.Count == 0)
        {
            return null;
        }

        return new AiModelStageTokenUsageSummary(
            usages.All(usage => usage.UsageReported),
            SumNullable(usages.Select(usage => usage.PromptTokens)),
            SumNullable(usages.Select(usage => usage.CompletionTokens)),
            SumNullable(usages.Select(usage => usage.TotalTokens)),
            SumNullable(usages.Select(usage =>
                usage.PromptCacheHitTokens)),
            SumNullable(usages.Select(usage =>
                usage.PromptCacheMissTokens)),
            SumNullable(usages.Select(usage => usage.ReasoningTokens)),
            usages.Count);
    }

    private static int? SumNullable(IEnumerable<int?> values)
    {
        List<int> availableValues = values.OfType<int>().ToList();
        return availableValues.Count == 0
            ? null
            : availableValues.Sum();
    }
}

/// <summary>
/// 聚合同一阶段一次或多次尝试返回的 Token 用量。
/// </summary>
public sealed record AiModelStageTokenUsageSummary(
    bool UsageComplete,
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    int? PromptCacheHitTokens,
    int? PromptCacheMissTokens,
    int? ReasoningTokens,
    int AttemptCount);

/// <summary>
/// 表示一条 AI 消息所属群级、单人和生成调用的用量摘要。
/// </summary>
public sealed record AiMessageTokenUsageSummary(
    AiModelStageTokenUsageSummary? GroupDirector,
    AiModelStageTokenUsageSummary? ConversationDirector,
    AiModelStageTokenUsageSummary? ReplyGeneration,
    AiModelStageTokenUsageSummary? SelfMemoryJudgment,
    AiModelStageTokenUsageSummary? WorldKnowledgeExtraction,
    bool UsageComplete,
    int TotalTokens,
    int InteractionSharedMessageCount,
    int ResponseSharedMessageCount);

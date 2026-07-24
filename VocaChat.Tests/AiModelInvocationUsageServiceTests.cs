using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证模型供应商返回的真实 Token 用量能够保存，并按消息批次正确聚合。
/// </summary>
public sealed class AiModelInvocationUsageServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task ChatClient_WithDeepSeekUsage_PersistsAllReportedTokenDetails()
    {
        TokenUsageResponseHandler handler = new();
        using HttpClient httpClient = new(handler);
        AiModelInvocationUsageService usageService = new(
            _database.CreateDbContextFactory());
        OpenAiCompatibleChatClient client = new(
            httpClient,
            new AiMessageGenerationOptions
            {
                BaseUrl = "https://api.example/v1/",
                Model = "deepseek-v4-flash"
            },
            connectionSettingsService: null,
            usageService);
        Guid responseBatchId = Guid.NewGuid();

        string? content = await client.CompleteJsonAsync(
            "system",
            "user",
            temperature: 0.2,
            topP: 0.8,
            maximumCompletionTokens: 64,
            invocationContext: new AiModelInvocationContext
            {
                Stage = AiModelInvocationStage.ReplyGeneration,
                AttemptNumber = 2,
                AiResponseBatchId = responseBatchId
            });

        Assert.Equal("{}", content);
        using VocaChat.Data.VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        AiModelInvocationUsage usage = dbContext
            .AiModelInvocationUsages
            .AsNoTracking()
            .Single();
        Assert.Equal(AiModelInvocationStage.ReplyGeneration, usage.Stage);
        Assert.Equal("deepseek-v4-flash", usage.ModelName);
        Assert.Equal(responseBatchId, usage.AiResponseBatchId);
        Assert.Equal(2, usage.AttemptNumber);
        Assert.True(usage.UsageReported);
        Assert.Equal(120, usage.PromptTokens);
        Assert.Equal(30, usage.CompletionTokens);
        Assert.Equal(150, usage.TotalTokens);
        Assert.Equal(80, usage.PromptCacheHitTokens);
        Assert.Equal(40, usage.PromptCacheMissTokens);
        Assert.Equal(12, usage.ReasoningTokens);
    }

    [Fact]
    public async Task ChatClient_WithOllamaUsage_PersistsNativeTokenCounts()
    {
        OllamaTokenUsageResponseHandler handler = new();
        using HttpClient httpClient = new(handler);
        AiModelInvocationUsageService usageService = new(
            _database.CreateDbContextFactory());
        OpenAiCompatibleChatClient client = new(
            httpClient,
            new AiMessageGenerationOptions
            {
                BaseUrl = "http://127.0.0.1:11434/api/",
                Model = "vocachat-qwen3.5-4b"
            },
            connectionSettingsService: null,
            usageService);

        string? content = await client.CompleteJsonAsync(
            "system",
            "user",
            temperature: 0.2,
            topP: 0.8,
            maximumCompletionTokens: 64,
            invocationContext: new AiModelInvocationContext
            {
                Stage = AiModelInvocationStage.GroupDirector,
                AttemptNumber = 1,
                InteractionBatchId = Guid.NewGuid()
            });

        Assert.Equal("{}", content);
        using VocaChat.Data.VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        AiModelInvocationUsage usage = dbContext
            .AiModelInvocationUsages
            .AsNoTracking()
            .Single();
        Assert.Equal("vocachat-qwen3.5-4b", usage.ModelName);
        Assert.True(usage.UsageReported);
        Assert.Equal(90, usage.PromptTokens);
        Assert.Equal(18, usage.CompletionTokens);
        Assert.Equal(108, usage.TotalTokens);
        Assert.Null(usage.PromptCacheHitTokens);
        Assert.Null(usage.PromptCacheMissTokens);
        Assert.Null(usage.ReasoningTokens);
    }

    [Fact]
    public void GroupMessages_SharedBatchesReuseActualUsageWithoutDuplicatingIt()
    {
        AiModelInvocationUsageService usageService = new(
            _database.CreateDbContextFactory());
        Guid groupChatId = Guid.NewGuid();
        Guid aiAccountId = Guid.NewGuid();
        Guid interactionBatchId = Guid.NewGuid();
        Guid responseBatchId = Guid.NewGuid();
        AiModelUsageCorrelation correlation = new()
        {
            GroupChatId = groupChatId,
            InteractionBatchId = interactionBatchId,
            AiResponseBatchId = responseBatchId
        };

        Assert.True(usageService.TryRecord(
            correlation.CreateInvocationContext(
                AiModelInvocationStage.GroupDirector,
                attemptNumber: 1),
            "director-model",
            new AiModelTokenUsage(100, 20, 120, null, null, null)));
        Assert.True(usageService.TryRecord(
            correlation.CreateInvocationContext(
                AiModelInvocationStage.ConversationDirector,
                attemptNumber: 1,
                aiAccountId),
            "director-model",
            new AiModelTokenUsage(60, 10, 70, null, null, null)));
        Assert.True(usageService.TryRecord(
            correlation.CreateInvocationContext(
                AiModelInvocationStage.ReplyGeneration,
                attemptNumber: 1,
                aiAccountId),
            "reply-model",
            new AiModelTokenUsage(80, 30, 110, null, null, null)));
        Assert.True(usageService.TryRecord(
            correlation.CreateInvocationContext(
                AiModelInvocationStage.SelfMemoryJudgment,
                attemptNumber: 1,
                aiAccountId),
            "memory-judge-model",
            new AiModelTokenUsage(40, 10, 50, null, null, null)));
        Assert.True(usageService.TryRecord(
            correlation.CreateInvocationContext(
                AiModelInvocationStage.WorldKnowledgeExtraction,
                attemptNumber: 1,
                aiAccountId),
            "knowledge-extractor-model",
            new AiModelTokenUsage(20, 5, 25, null, null, null)));

        GroupMessage firstMessage = CreateAiMessage(
            groupChatId,
            aiAccountId,
            interactionBatchId,
            responseBatchId,
            "第一条");
        GroupMessage secondMessage = CreateAiMessage(
            groupChatId,
            aiAccountId,
            interactionBatchId,
            responseBatchId,
            "第二条");

        IReadOnlyDictionary<Guid, AiMessageTokenUsageSummary> summaries =
            usageService.GetForGroupMessages(
                new[] { firstMessage, secondMessage });

        Assert.Equal(2, summaries.Count);
        foreach (AiMessageTokenUsageSummary summary in summaries.Values)
        {
            Assert.Equal(375, summary.TotalTokens);
            Assert.NotNull(summary.SelfMemoryJudgment);
            Assert.Equal(50, summary.SelfMemoryJudgment.TotalTokens);
            Assert.NotNull(summary.WorldKnowledgeExtraction);
            Assert.Equal(
                25,
                summary.WorldKnowledgeExtraction.TotalTokens);
            Assert.Equal(2, summary.InteractionSharedMessageCount);
            Assert.Equal(2, summary.ResponseSharedMessageCount);
            Assert.True(summary.UsageComplete);
        }

        using VocaChat.Data.VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(5, dbContext.AiModelInvocationUsages.Count());
    }

    private static GroupMessage CreateAiMessage(
        Guid groupChatId,
        Guid aiAccountId,
        Guid interactionBatchId,
        Guid responseBatchId,
        string content) =>
        new(
            groupChatId,
            MessageSenderType.AiAccount,
            "小语",
            aiAccountId,
            content,
            DateTime.UtcNow,
            interactionBatchId: interactionBatchId,
            aiResponseBatchId: responseBatchId);

    public void Dispose()
    {
        _database.Dispose();
    }

    private sealed class TokenUsageResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            const string responseJson = """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{}"
                      }
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 120,
                    "completion_tokens": 30,
                    "total_tokens": 150,
                    "prompt_cache_hit_tokens": 80,
                    "prompt_cache_miss_tokens": 40,
                    "completion_tokens_details": {
                      "reasoning_tokens": 12
                    }
                  }
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseJson,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private sealed class OllamaTokenUsageResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            const string responseJson = """
                {
                  "message": {
                    "role": "assistant",
                    "content": "{}"
                  },
                  "prompt_eval_count": 90,
                  "eval_count": 18
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseJson,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}

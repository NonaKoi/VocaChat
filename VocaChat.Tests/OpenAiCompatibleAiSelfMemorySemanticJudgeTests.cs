using System.Net;
using System.Text;
using System.Text.Json;
using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证个人记忆语义判断器的结构化输出、上下文边界和安全回退。
/// </summary>
public sealed class OpenAiCompatibleAiSelfMemorySemanticJudgeTests
{
    [Fact]
    public async Task JudgeAsync_ParsesDecisionAndIncludesSpeakerWorldAndEvidence()
    {
        AiSelfMemorySemanticJudgmentRequest request = CreateRequest();
        string decisionJson = JsonSerializer.Serialize(new
        {
            decisions = new[]
            {
                new
                {
                    proposalIndex = 0,
                    outcome = "Accept",
                    targetMemoryId = (string?)null,
                    factKey = "ongoing.autumn-exhibition",
                    factNature = "Objective",
                    mutability = "Evolving",
                    reason = "消息明确表达了当前持续事项"
                }
            }
        });
        RecordingHandler handler = new(CreateResponse(decisionJson));

        AiSelfMemorySemanticJudgmentResult result =
            await CreateJudge(handler).JudgeAsync(request);

        Assert.False(result.UsedFallback);
        AiSelfMemorySemanticDecision decision =
            Assert.Single(result.Decisions);
        Assert.Equal(AiSelfMemorySemanticOutcome.Accept, decision.Outcome);
        Assert.Equal("ongoing.autumn-exhibition", decision.FactKey);
        Assert.Equal(AiSelfMemoryFactNature.Objective, decision.FactNature);
        Assert.Equal(AiSelfMemoryMutability.Evolving, decision.Mutability);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains(request.Speaker.Id.ToString(), userPrompt);
        Assert.Contains(
            request.Speaker.CharacterWorldId.ToString(),
            userPrompt);
        Assert.Contains(
            request.SavedMessages[0].MessageId.ToString(),
            userPrompt);
        Assert.Contains("正在准备秋季插画展", userPrompt);
    }

    [Fact]
    public async Task JudgeAsync_WhenAcceptChangesFactKey_RetriesWithProposalFactKey()
    {
        string driftedDecision = JsonSerializer.Serialize(new
        {
            decisions = new[]
            {
                new
                {
                    proposalIndex = 0,
                    outcome = "Accept",
                    targetMemoryId = (string?)null,
                    factKey = "experience.mist-island-cafe",
                    factNature = "Objective",
                    mutability = "Evolving",
                    reason = "错误地改到了无关事实键"
                }
            }
        });
        string correctedDecision = JsonSerializer.Serialize(new
        {
            decisions = new[]
            {
                new
                {
                    proposalIndex = 0,
                    outcome = "Accept",
                    targetMemoryId = (string?)null,
                    factKey = "ongoing.autumn-exhibition",
                    factNature = "Objective",
                    mutability = "Evolving",
                    reason = "沿用候选事实键"
                }
            }
        });
        RecordingHandler handler = new(
            CreateResponse(driftedDecision),
            CreateResponse(correctedDecision));

        AiSelfMemorySemanticJudgmentResult result =
            await CreateJudge(handler, outputValidationRetryCount: 1)
                .JudgeAsync(CreateRequest());

        Assert.False(result.UsedFallback);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(
            "ongoing.autumn-exhibition",
            Assert.Single(result.Decisions).FactKey);
        using JsonDocument retryBody = JsonDocument.Parse(
            handler.RequestBodies[1]);
        string retryPrompt = retryBody.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("不能把候选改写到无关事实键", retryPrompt);
    }

    [Fact]
    public async Task JudgeAsync_InvalidOutputAfterRetry_KeepsProposalPending()
    {
        RecordingHandler handler = new(
            CreateResponse("""{"decisions":[]}"""),
            CreateResponse("""{"decisions":[]}"""));

        AiSelfMemorySemanticJudgmentResult result =
            await CreateJudge(handler, outputValidationRetryCount: 1)
                .JudgeAsync(CreateRequest());

        Assert.True(result.UsedFallback);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(
            AiSelfMemorySemanticOutcome.Pending,
            Assert.Single(result.Decisions).Outcome);
        Assert.Contains("输出无效", result.FallbackReason);
    }

    [Fact]
    public async Task JudgeAsync_SupersedeWithoutTarget_KeepsProposalPending()
    {
        string decisionJson = JsonSerializer.Serialize(new
        {
            decisions = new[]
            {
                new
                {
                    proposalIndex = 0,
                    outcome = "Supersede",
                    targetMemoryId = (string?)null,
                    factKey = "ongoing.autumn-exhibition",
                    factNature = "Objective",
                    mutability = "Evolving",
                    reason = "尝试替代但没有目标"
                }
            }
        });
        RecordingHandler handler = new(CreateResponse(decisionJson));

        AiSelfMemorySemanticJudgmentResult result =
            await CreateJudge(handler).JudgeAsync(CreateRequest());

        Assert.True(result.UsedFallback);
        Assert.Equal(
            AiSelfMemorySemanticOutcome.Pending,
            Assert.Single(result.Decisions).Outcome);
    }

    private static OpenAiCompatibleAiSelfMemorySemanticJudge CreateJudge(
        HttpMessageHandler handler,
        int outputValidationRetryCount = 0)
    {
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("https://api.example.test/")
        };
        AiMessageGenerationOptions options = new()
        {
            BaseUrl = "https://api.example.test/v1/",
            Model = "memory-judge-test-model",
            OutputValidationRetryCount = outputValidationRetryCount
        };

        return new OpenAiCompatibleAiSelfMemorySemanticJudge(
            new OpenAiCompatibleChatClient(client, options),
            options);
    }

    private static AiSelfMemorySemanticJudgmentRequest CreateRequest()
    {
        AiAccount speaker = new(
            "1234567",
            "小语",
            "正在筹备个人插画展的朋友",
            "认真",
            "自然简短");
        AiSelfMemoryProposal proposal = new(
            AiSelfMemoryProposalOperation.Add,
            TargetMemoryId: null,
            speaker.Id,
            speaker.CharacterWorldId,
            AiSelfMemoryType.OngoingActivity,
            "ongoing.autumn-exhibition",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Evolving,
            "正在准备秋季插画展",
            "本轮消息明确说明了持续事项");
        AiPersistedMessageEvidence evidence = new(
            Guid.NewGuid(),
            "我最近正在准备秋季插画展",
            new DateTime(2026, 7, 23, 10, 0, 0));

        return new AiSelfMemorySemanticJudgmentRequest(
            speaker,
            "现实世界",
            "遵循常识的现代现实世界。",
            new[] { proposal },
            Array.Empty<AiConversationSelfMemory>(),
            new[] { evidence },
            UsageCorrelation: null);
    }

    private static HttpResponseMessage CreateResponse(string content)
    {
        string responseBody = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new { content }
                }
            }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                responseBody,
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public int CallCount { get; private set; }
        public List<string> RequestBodies { get; } = new();

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}

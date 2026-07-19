using System.Net;
using System.Text;
using System.Text.Json;
using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证 Session 洞察模型输出的方向、证据、候选过滤和安全回退。
/// </summary>
public sealed class OpenAiCompatibleSessionInsightAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_ParsesTwoFixedDirectionsAndEvidence()
    {
        SessionInsightAnalysisRequest request = CreateRequest();
        Guid initiatorMessageId = request.Messages[0].Id;
        Guid recipientMessageId = request.Messages[1].Id;
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            CreateInsightJson(
                new
                {
                    affinity = Signal("Positive", "Medium"),
                    trust = Signal("Neutral", "None"),
                    reason = "接收者分享了自己的偏好，交流更亲近",
                    relationshipEvidenceMessageIds = new[]
                    {
                        recipientMessageId
                    },
                    memories = new object[]
                    {
                        Memory(
                            "Preference",
                            "接收者喜欢雨天散步",
                            "High",
                            recipientMessageId)
                    }
                },
                new
                {
                    affinity = Signal("Negative", "Low"),
                    trust = Signal("Neutral", "None"),
                    reason = "发起者最初否定了这个爱好",
                    relationshipEvidenceMessageIds = new[]
                    {
                        initiatorMessageId
                    },
                    memories = Array.Empty<object>()
                })));
        OpenAiCompatibleSessionInsightAnalyzer analyzer = CreateAnalyzer(handler);

        SessionInsightAnalysis analysis = await analyzer.AnalyzeAsync(request);

        Assert.False(analysis.UsedFallback);
        Assert.Equal(
            RelationshipSignalPolarity.Positive,
            analysis.InitiatorPerspective.AffinityPolarity);
        Assert.Equal(
            RelationshipSignalStrength.Medium,
            analysis.InitiatorPerspective.AffinityStrength);
        SessionMemoryCandidate memory = Assert.Single(
            analysis.InitiatorPerspective.MemoryCandidates);
        Assert.Equal(AiMemoryType.Preference, memory.Type);
        Assert.Equal("接收者喜欢雨天散步", memory.Summary);
        Assert.Equal(recipientMessageId, Assert.Single(memory.EvidenceMessageIds));
        Assert.Equal(
            RelationshipSignalPolarity.Negative,
            analysis.RecipientPerspective.AffinityPolarity);
        Assert.Contains(recipientMessageId.ToString(), handler.RequestBody);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_DropsMemoryWithoutSubjectEvidence()
    {
        SessionInsightAnalysisRequest request = CreateRequest();
        Guid initiatorMessageId = request.Messages[0].Id;
        Guid recipientMessageId = request.Messages[1].Id;
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            CreateInsightJson(
                NeutralDirection(new object[]
                {
                    Memory(
                        "Preference",
                        "接收者喜欢不存在的项目",
                        "High",
                        initiatorMessageId),
                    Memory(
                        "Preference",
                        "接收者喜欢雨天散步",
                        "Medium",
                        recipientMessageId)
                }),
                NeutralDirection(Array.Empty<object>()))));

        SessionInsightAnalysis analysis = await CreateAnalyzer(handler)
            .AnalyzeAsync(request);

        SessionMemoryCandidate validMemory = Assert.Single(
            analysis.InitiatorPerspective.MemoryCandidates);
        Assert.Equal("接收者喜欢雨天散步", validMemory.Summary);
    }

    [Fact]
    public async Task AnalyzeAsync_RetriesInvalidRelationshipEvidence()
    {
        SessionInsightAnalysisRequest request = CreateRequest();
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                CreateInsightJson(
                    new
                    {
                        affinity = Signal("Positive", "High"),
                        trust = Signal("Neutral", "None"),
                        reason = "缺少证据",
                        relationshipEvidenceMessageIds = Array.Empty<Guid>(),
                        memories = Array.Empty<object>()
                    },
                    NeutralDirection(Array.Empty<object>()))),
            CreateResponse(
                HttpStatusCode.OK,
                CreateInsightJson(
                    NeutralDirection(Array.Empty<object>()),
                    NeutralDirection(Array.Empty<object>()))));

        SessionInsightAnalysis analysis = await CreateAnalyzer(
                handler,
                outputValidationRetryCount: 1)
            .AnalyzeAsync(request);

        Assert.False(analysis.UsedFallback);
        Assert.Equal(
            RelationshipSignalPolarity.Neutral,
            analysis.InitiatorPerspective.AffinityPolarity);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenModelUnavailable_ReturnsSafeFallback()
    {
        RecordingHandler handler = new(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        SessionInsightAnalysis analysis = await CreateAnalyzer(handler)
            .AnalyzeAsync(CreateRequest());

        Assert.True(analysis.UsedFallback);
        Assert.Empty(analysis.InitiatorPerspective.MemoryCandidates);
        Assert.Equal(
            RelationshipSignalPolarity.Neutral,
            analysis.RecipientPerspective.TrustPolarity);
    }

    private static OpenAiCompatibleSessionInsightAnalyzer CreateAnalyzer(
        HttpMessageHandler handler,
        int outputValidationRetryCount = 0)
    {
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("http://localhost:11434/v1/")
        };
        AiMessageGenerationOptions options = new()
        {
            Model = "vocachat-test-model",
            OutputValidationRetryCount = outputValidationRetryCount
        };
        return new OpenAiCompatibleSessionInsightAnalyzer(
            new OpenAiCompatibleChatClient(client, options),
            options);
    }

    private static SessionInsightAnalysisRequest CreateRequest()
    {
        AiAccount initiator = new(
            "1234567",
            "小语",
            "喜欢直接表达的朋友",
            "坦率",
            "简短");
        AiAccount recipient = new(
            "7654321",
            "小北",
            "喜欢安静生活的朋友",
            "温和",
            "自然");
        Guid privateChatId = Guid.NewGuid();
        AutonomousPrivateChatSession session = new(
            privateChatId,
            initiator.Id,
            recipient.Id,
            "雨天安排",
            maximumRounds: 3,
            continuationRatePercent: 80,
            new DateTime(2026, 7, 19, 22, 0, 0));
        PrivateMessage initiatorMessage = new(
            privateChatId,
            MessageSenderType.AiAccount,
            initiator.Nickname,
            initiator.Id,
            "我不太理解为什么有人喜欢下雨天",
            new DateTime(2026, 7, 19, 22, 1, 0),
            session.Id);
        PrivateMessage recipientMessage = new(
            privateChatId,
            MessageSenderType.AiAccount,
            recipient.Nickname,
            recipient.Id,
            "我其实很喜欢雨天散步，街上会安静很多",
            new DateTime(2026, 7, 19, 22, 2, 0),
            session.Id);
        return new SessionInsightAnalysisRequest(
            session,
            initiator,
            recipient,
            new[] { initiatorMessage, recipientMessage });
    }

    private static object NeutralDirection(object[] memories) => new
    {
        affinity = Signal("Neutral", "None"),
        trust = Signal("Neutral", "None"),
        reason = "没有明确关系事件",
        relationshipEvidenceMessageIds = Array.Empty<Guid>(),
        memories
    };

    private static object Signal(string polarity, string strength) => new
    {
        polarity,
        strength
    };

    private static object Memory(
        string type,
        string summary,
        string importance,
        Guid evidenceMessageId) => new
    {
        type,
        summary,
        importance,
        evidenceMessageIds = new[] { evidenceMessageId }
    };

    private static string CreateInsightJson(
        object initiatorPerspective,
        object recipientPerspective)
    {
        return JsonSerializer.Serialize(new
        {
            initiatorPerspective,
            recipientPerspective
        });
    }

    private static HttpResponseMessage CreateResponse(
        HttpStatusCode statusCode,
        string generatedContent)
    {
        string responseBody = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = generatedContent
                    }
                }
            }
        });
        return new HttpResponseMessage(statusCode)
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

        public string RequestBody { get; private set; } = string.Empty;
        public int CallCount { get; private set; }

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responses.Dequeue();
        }
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using VocaChat.Services;

namespace VocaChat.Tests;

public sealed class OpenAiCompatibleAiWorldKnowledgeSemanticExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ParsesGroundedConceptFromMessage()
    {
        const string sourceContent =
            "潮汐门的钟声只在蓝月落下时响起。";
        RecordingHandler handler = new(CreateResponse(
            """
            {
              "signal": "UnfamiliarConcept",
              "concepts": [
                { "name": "潮汐门", "category": "Place" }
              ]
            }
            """));

        AiWorldKnowledgeSemanticExtractionResult result =
            await CreateExtractor(handler).ExtractAsync(
                new AiWorldKnowledgeSemanticExtractionRequest(
                    sourceContent,
                    Guid.NewGuid(),
                    UsageCorrelation: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            AiWorldKnowledgeSignal.UnfamiliarConcept,
            result.Signal);
        AiWorldKnowledgeSemanticConcept concept =
            Assert.Single(result.Concepts);
        Assert.Equal("潮汐门", concept.Name);
        Assert.Equal(AiWorldKnowledgeConceptCategory.Place, concept.Category);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ExtractAsync_WhenConceptIsNotLiteralSubstring_ReturnsFailure()
    {
        RecordingHandler handler = new(CreateResponse(
            """
            {
              "signal": "UnfamiliarConcept",
              "concepts": [
                { "name": "模型补写的地点", "category": "Place" }
              ]
            }
            """));

        AiWorldKnowledgeSemanticExtractionResult result =
            await CreateExtractor(handler).ExtractAsync(
                new AiWorldKnowledgeSemanticExtractionRequest(
                    "她说钟声会在蓝月落下时响起。",
                    Guid.NewGuid(),
                    UsageCorrelation: null));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Concepts);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ExtractAsync_WhenConceptHasModelFormatting_KeepsOnlyGroundedText()
    {
        RecordingHandler handler = new(CreateResponse(
            """
            {
              "signal": "UnfamiliarConcept",
              "concepts": [
                { "name": "潮汐门（地点）", "category": "Place" }
              ]
            }
            """));

        AiWorldKnowledgeSemanticExtractionResult result =
            await CreateExtractor(handler).ExtractAsync(
                new AiWorldKnowledgeSemanticExtractionRequest(
                    "潮汐门的钟声只在蓝月落下时响起。",
                    Guid.NewGuid(),
                    UsageCorrelation: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "潮汐门",
            Assert.Single(result.Concepts).Name);
    }

    private static OpenAiCompatibleAiWorldKnowledgeSemanticExtractor
        CreateExtractor(HttpMessageHandler handler)
    {
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("https://api.example.test/")
        };
        AiMessageGenerationOptions options = new()
        {
            BaseUrl = "https://api.example.test/v1/",
            Model = "world-knowledge-test-model",
            OutputValidationRetryCount = 0
        };

        return new OpenAiCompatibleAiWorldKnowledgeSemanticExtractor(
            new OpenAiCompatibleChatClient(client, options),
            options);
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

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }
}

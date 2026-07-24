using System.Net;
using System.Text;
using System.Text.Json;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证共享模型客户端能够根据明确的基础地址选择 OpenAI 兼容或 Ollama 原生传输。
/// </summary>
public sealed class OpenAiCompatibleChatClientTests
{
    [Fact]
    public async Task CompleteJsonAsync_WithOllamaApi_UsesNativeChatContract()
    {
        RecordingHandler remoteHandler = new("""
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"wrongTransport\":true}"
                  }
                }
              ]
            }
            """);
        RecordingHandler ollamaHandler = new("""
            {
              "model": "vocachat-qwen3.5-4b",
              "message": {
                "role": "assistant",
                "content": "{\"ok\":true}"
              },
              "prompt_eval_count": 20,
              "eval_count": 8
            }
            """);
        using HttpClient remoteClient = new(remoteHandler);
        using HttpClient ollamaClient = new(ollamaHandler);
        OpenAiCompatibleChatClient client = new(
            remoteClient,
            ollamaClient,
            new AiMessageGenerationOptions
            {
                BaseUrl = "http://127.0.0.1:11434/api/",
                Model = "vocachat-qwen3.5-4b"
            },
            connectionSettingsService: null,
            usageService: null);

        string? content = await client.CompleteJsonAsync(
            "system",
            "user",
            temperature: 0.3,
            topP: 0.75,
            maximumCompletionTokens: 256);

        Assert.Equal("{\"ok\":true}", content);
        Assert.Equal(
            "http://127.0.0.1:11434/api/chat",
            ollamaHandler.RequestUri?.AbsoluteUri);
        Assert.Equal(0, remoteHandler.CallCount);
        Assert.Equal(1, ollamaHandler.CallCount);

        using JsonDocument request = JsonDocument.Parse(
            ollamaHandler.RequestBody);
        JsonElement root = request.RootElement;
        Assert.False(root.GetProperty("think").GetBoolean());
        Assert.Equal("json", root.GetProperty("format").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(
            256,
            root.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.Equal(
            0.3,
            root.GetProperty("options").GetProperty("temperature").GetDouble());
        Assert.Equal(
            0.75,
            root.GetProperty("options").GetProperty("top_p").GetDouble());
        Assert.False(root.TryGetProperty("response_format", out _));
        Assert.False(root.TryGetProperty("thinking", out _));
    }

    [Fact]
    public async Task CompleteJsonAsync_WithOpenAiApi_PreservesCompatibleContract()
    {
        RecordingHandler handler = new("""
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"ok\":true}"
                  }
                }
              ]
            }
            """);
        using HttpClient httpClient = new(handler);
        OpenAiCompatibleChatClient client = new(
            httpClient,
            new AiMessageGenerationOptions
            {
                BaseUrl = "https://api.example.test/v1/",
                Model = "remote-model"
            });

        string? content = await client.CompleteJsonAsync(
            "system",
            "user",
            temperature: 0.2,
            topP: 0.8,
            maximumCompletionTokens: 64);

        Assert.Equal("{\"ok\":true}", content);
        Assert.Equal(
            "https://api.example.test/v1/chat/completions",
            handler.RequestUri?.AbsoluteUri);

        using JsonDocument request = JsonDocument.Parse(handler.RequestBody);
        JsonElement root = request.RootElement;
        Assert.Equal(
            "json_object",
            root.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Equal(
            "disabled",
            root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal(64, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("think", out _));
    }

    [Fact]
    public async Task CompleteJsonAsync_WhenOllamaContentIsBlank_RejectsResponse()
    {
        RecordingHandler handler = new("""
            {
              "message": {
                "role": "assistant",
                "content": ""
              },
              "prompt_eval_count": 20,
              "eval_count": 256
            }
            """);
        using HttpClient httpClient = new(handler);
        OpenAiCompatibleChatClient client = new(
            httpClient,
            new AiMessageGenerationOptions
            {
                BaseUrl = "http://127.0.0.1:11434/api/",
                Model = "vocachat-qwen3.5-4b"
            });

        AiMessageGenerationException exception =
            await Assert.ThrowsAsync<AiMessageGenerationException>(() =>
                client.CompleteJsonAsync(
                    "system",
                    "user",
                    temperature: 0.2,
                    topP: 0.8,
                    maximumCompletionTokens: 256));

        Assert.Contains("没有返回有效正文", exception.Message);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;
        public int CallCount { get; private set; }

        public RecordingHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}

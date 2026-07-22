using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VocaChat.Services;

/// <summary>
/// 封装 OpenAI 兼容 Chat Completions 的传输细节，供导演和消息生成器复用。
/// </summary>
public sealed class OpenAiCompatibleChatClient
{
    private readonly HttpClient _httpClient;
    private readonly AiMessageGenerationOptions _options;
    private readonly AiModelConnectionSettingsService? _connectionSettingsService;

    public OpenAiCompatibleChatClient(
        HttpClient httpClient,
        AiMessageGenerationOptions options)
        : this(httpClient, options, connectionSettingsService: null)
    {
    }

    public OpenAiCompatibleChatClient(
        HttpClient httpClient,
        AiMessageGenerationOptions options,
        AiModelConnectionSettingsService? connectionSettingsService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _connectionSettingsService = connectionSettingsService;
    }

    /// <summary>
    /// 请求一个严格 JSON 对象；关闭推理模式，避免把内部思考混入业务输出。
    /// </summary>
    public async Task<string?> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        double temperature,
        double topP,
        int maximumCompletionTokens,
        CancellationToken cancellationToken = default,
        Guid? aiAccountId = null)
    {
        AiModelConnection connection = ResolveConnection(aiAccountId);

        if (string.IsNullOrWhiteSpace(connection.Model))
        {
            throw new AiMessageGenerationException("未配置 AI 模型名称。");
        }

        if (_options.TimeoutSeconds <= 0 || maximumCompletionTokens <= 0)
        {
            throw new AiMessageGenerationException("AI 文本生成配置无效。");
        }

        using CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                BuildChatCompletionsUri(connection.BaseUrl))
            {
                Content = JsonContent.Create(new
                {
                    model = connection.Model,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    response_format = new { type = "json_object" },
                    thinking = new { type = "disabled" },
                    temperature,
                    top_p = topP,
                    max_tokens = maximumCompletionTokens,
                    stream = false
                })
            };

            if (!string.IsNullOrWhiteSpace(connection.ApiKey))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", connection.ApiKey);
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw new AiMessageGenerationException(
                    $"AI 文本生成服务返回了错误状态 {(int)response.StatusCode}。");
            }

            await using Stream responseStream = await response.Content
                .ReadAsStreamAsync(timeoutSource.Token);
            using JsonDocument responseDocument = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: timeoutSource.Token);

            return responseDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (AiMessageGenerationException)
        {
            throw;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiMessageGenerationException(
                "连接 AI 文本生成服务超时。",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiMessageGenerationException(
                "无法连接到 AI 文本生成服务。",
                exception);
        }
        catch (JsonException exception)
        {
            throw new AiMessageGenerationException(
                "AI 文本生成服务返回了无法解析的响应。",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new AiMessageGenerationException(
                "AI 文本生成服务返回的响应缺少必要字段。",
                exception);
        }
    }

    private AiModelConnection ResolveConnection(Guid? aiAccountId)
    {
        if (_connectionSettingsService is null)
        {
            return new AiModelConnection(
                _options.BaseUrl,
                _options.Model,
                _options.ApiKey);
        }

        return aiAccountId.HasValue
            ? _connectionSettingsService.ResolveForAccount(aiAccountId.Value)
            : _connectionSettingsService.ResolveGlobal();
    }

    private static Uri BuildChatCompletionsUri(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp
                && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new AiMessageGenerationException("AI 接口地址无效。");
        }

        string normalizedBaseUrl = baseUrl.EndsWith('/')
            ? baseUrl
            : $"{baseUrl}/";
        return new Uri(new Uri(normalizedBaseUrl), "chat/completions");
    }
}

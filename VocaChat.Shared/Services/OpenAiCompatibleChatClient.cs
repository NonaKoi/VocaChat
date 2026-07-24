using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VocaChat.Services;

/// <summary>
/// 封装 OpenAI 兼容 Chat Completions 和 Ollama 原生 Chat 的传输细节，
/// 供导演和消息生成器复用。
/// </summary>
public sealed class OpenAiCompatibleChatClient
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _ollamaHttpClient;
    private readonly AiMessageGenerationOptions _options;
    private readonly AiModelConnectionSettingsService? _connectionSettingsService;
    private readonly AiModelInvocationUsageService? _usageService;

    public OpenAiCompatibleChatClient(
        HttpClient httpClient,
        AiMessageGenerationOptions options)
        : this(
            httpClient,
            options,
            connectionSettingsService: null,
            usageService: null)
    {
    }

    public OpenAiCompatibleChatClient(
        HttpClient httpClient,
        AiMessageGenerationOptions options,
        AiModelConnectionSettingsService? connectionSettingsService)
        : this(
            httpClient,
            options,
            connectionSettingsService,
            usageService: null)
    {
    }

    public OpenAiCompatibleChatClient(
        HttpClient httpClient,
        AiMessageGenerationOptions options,
        AiModelConnectionSettingsService? connectionSettingsService,
        AiModelInvocationUsageService? usageService)
        : this(
            httpClient,
            httpClient,
            options,
            connectionSettingsService,
            usageService)
    {
    }

    public OpenAiCompatibleChatClient(
        HttpClient httpClient,
        HttpClient ollamaHttpClient,
        AiMessageGenerationOptions options,
        AiModelConnectionSettingsService? connectionSettingsService,
        AiModelInvocationUsageService? usageService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ollamaHttpClient = ollamaHttpClient
            ?? throw new ArgumentNullException(nameof(ollamaHttpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _connectionSettingsService = connectionSettingsService;
        _usageService = usageService;
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
        Guid? aiAccountId = null,
        AiModelInvocationContext? invocationContext = null)
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
            bool usesOllamaNativeApi = UsesOllamaNativeApi(
                connection.BaseUrl);
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                usesOllamaNativeApi
                    ? BuildOllamaChatUri(connection.BaseUrl)
                    : BuildChatCompletionsUri(connection.BaseUrl))
            {
                Content = usesOllamaNativeApi
                    ? CreateOllamaRequestContent(
                        connection.Model,
                        systemPrompt,
                        userPrompt,
                        temperature,
                        topP,
                        maximumCompletionTokens)
                    : CreateOpenAiRequestContent(
                        connection.Model,
                        systemPrompt,
                        userPrompt,
                        temperature,
                        topP,
                        maximumCompletionTokens)
            };

            if (!string.IsNullOrWhiteSpace(connection.ApiKey))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", connection.ApiKey);
            }

            HttpClient transportClient = usesOllamaNativeApi
                ? _ollamaHttpClient
                : _httpClient;
            using HttpResponseMessage response = await transportClient.SendAsync(
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
            JsonElement root = responseDocument.RootElement;

            if (invocationContext is not null)
            {
                _usageService?.TryRecord(
                    invocationContext,
                    connection.Model,
                    usesOllamaNativeApi
                        ? ParseOllamaUsage(root)
                        : ParseOpenAiUsage(root));
            }

            string? content = usesOllamaNativeApi
                ? ParseOllamaContent(root)
                : ParseOpenAiContent(root);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiMessageGenerationException(
                    "AI 文本生成服务没有返回有效正文。");
            }

            return content;
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

    private static JsonContent CreateOpenAiRequestContent(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        double topP,
        int maximumCompletionTokens)
    {
        return JsonContent.Create(new
        {
            model,
            messages = CreateMessages(systemPrompt, userPrompt),
            response_format = new { type = "json_object" },
            thinking = new { type = "disabled" },
            temperature,
            top_p = topP,
            max_tokens = maximumCompletionTokens,
            stream = false
        });
    }

    private static JsonContent CreateOllamaRequestContent(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        double topP,
        int maximumCompletionTokens)
    {
        return JsonContent.Create(new
        {
            model,
            messages = CreateMessages(systemPrompt, userPrompt),
            stream = false,
            think = false,
            format = "json",
            options = new
            {
                temperature,
                top_p = topP,
                num_predict = maximumCompletionTokens
            }
        });
    }

    private static object[] CreateMessages(
        string systemPrompt,
        string userPrompt) =>
        new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        };

    private static string? ParseOpenAiContent(JsonElement root) =>
        root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

    private static string? ParseOllamaContent(JsonElement root) =>
        root
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

    private static AiModelTokenUsage? ParseOpenAiUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out JsonElement usage)
            || usage.ValueKind != JsonValueKind.Object
            || !TryGetNonNegativeInt(
                usage,
                "prompt_tokens",
                out int promptTokens)
            || !TryGetNonNegativeInt(
                usage,
                "completion_tokens",
                out int completionTokens)
            || !TryGetNonNegativeInt(
                usage,
                "total_tokens",
                out int totalTokens))
        {
            return null;
        }

        int? reasoningTokens = null;
        if (usage.TryGetProperty(
                "completion_tokens_details",
                out JsonElement completionDetails)
            && completionDetails.ValueKind == JsonValueKind.Object
            && TryGetNonNegativeInt(
                completionDetails,
                "reasoning_tokens",
                out int parsedReasoningTokens))
        {
            reasoningTokens = parsedReasoningTokens;
        }

        return new AiModelTokenUsage(
            promptTokens,
            completionTokens,
            totalTokens,
            GetOptionalNonNegativeInt(
                usage,
                "prompt_cache_hit_tokens"),
            GetOptionalNonNegativeInt(
                usage,
                "prompt_cache_miss_tokens"),
            reasoningTokens);
    }

    private static AiModelTokenUsage? ParseOllamaUsage(JsonElement root)
    {
        if (!TryGetNonNegativeInt(
                root,
                "prompt_eval_count",
                out int promptTokens)
            || !TryGetNonNegativeInt(
                root,
                "eval_count",
                out int completionTokens))
        {
            return null;
        }

        return new AiModelTokenUsage(
            promptTokens,
            completionTokens,
            promptTokens + completionTokens,
            PromptCacheHitTokens: null,
            PromptCacheMissTokens: null,
            ReasoningTokens: null);
    }

    private static int? GetOptionalNonNegativeInt(
        JsonElement element,
        string propertyName) =>
        TryGetNonNegativeInt(element, propertyName, out int value)
            ? value
            : null;

    private static bool TryGetNonNegativeInt(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value)
            && value >= 0;
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

    private static bool UsesOllamaNativeApi(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            return false;
        }

        return baseUri.AbsolutePath
            .TrimEnd('/')
            .EndsWith("/api", StringComparison.OrdinalIgnoreCase);
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

    private static Uri BuildOllamaChatUri(string baseUrl)
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
        return new Uri(new Uri(normalizedBaseUrl), "chat");
    }
}

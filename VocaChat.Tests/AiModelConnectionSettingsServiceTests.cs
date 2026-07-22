using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证全局/账号专有模型连接的持久化、密钥保护与运行时解析。
/// </summary>
public sealed class AiModelConnectionSettingsServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly AiMessageGenerationOptions _hostDefaults = new()
    {
        BaseUrl = "http://127.0.0.1:11434/v1/",
        Model = "host-model",
        ApiKey = "host-secret"
    };

    [Fact]
    public void GlobalSettings_PersistEncryptedAndReload()
    {
        AiModelConnectionSettingsService service = CreateService();

        bool succeeded = service.TryUpdateGlobalSettings(
            "https://global.example/v1",
            "global-model",
            "global-secret",
            clearApiKey: false,
            out AiModelConnectionSettingsSnapshot? saved,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(saved);
        Assert.True(saved.HasApiKey);

        using (VocaChat.Data.VocaChatDbContext dbContext =
               _database.CreateDbContextFactory().CreateDbContext())
        {
            AiModelConnectionSettings stored = dbContext
                .AiModelConnectionSettings
                .AsNoTracking()
                .Single();
            Assert.NotEqual("global-secret", stored.ProtectedApiKey);
            Assert.DoesNotContain("global-secret", stored.ProtectedApiKey!);
        }

        AiModelConnection resolved = CreateService().ResolveGlobal();
        Assert.Equal("https://global.example/v1/", resolved.BaseUrl);
        Assert.Equal("global-model", resolved.Model);
        Assert.Equal("global-secret", resolved.ApiKey);
    }

    [Fact]
    public void AccountSettings_DedicatedConnectionOverridesGlobalUntilInheritanceIsEnabled()
    {
        AiAccount account = CreateAccount("DedicatedModelFriend");
        AiModelConnectionSettingsService service = CreateService();
        Assert.True(service.TryUpdateGlobalSettings(
            "https://global.example/v1/",
            "global-model",
            "global-secret",
            clearApiKey: false,
            out _,
            out string globalError), globalError);

        Assert.True(service.TryUpdateAccountSettings(
            account.Id,
            useGlobalSettings: false,
            "https://friend.example/v1/",
            "friend-model",
            "friend-secret",
            clearApiKey: false,
            out AiAccountModelConnectionSettingsSnapshot? dedicated,
            out string dedicatedError), dedicatedError);

        Assert.NotNull(dedicated);
        Assert.False(dedicated.UseGlobalSettings);
        AiModelConnection resolvedDedicated = service.ResolveForAccount(account.Id);
        Assert.Equal("friend-model", resolvedDedicated.Model);
        Assert.Equal("friend-secret", resolvedDedicated.ApiKey);

        Assert.True(service.TryUpdateAccountSettings(
            account.Id,
            useGlobalSettings: true,
            dedicated.BaseUrl,
            dedicated.Model,
            apiKey: null,
            clearApiKey: false,
            out AiAccountModelConnectionSettingsSnapshot? inherited,
            out string inheritError), inheritError);

        Assert.NotNull(inherited);
        Assert.True(inherited.UseGlobalSettings);
        Assert.Equal("global-model", inherited.EffectiveModel);
        Assert.Equal("global-secret", service.ResolveForAccount(account.Id).ApiKey);
    }

    [Fact]
    public async Task ChatClient_WithAccountId_UsesDedicatedAddressModelAndApiKey()
    {
        AiAccount account = CreateAccount("RuntimeModelFriend");
        AiModelConnectionSettingsService service = CreateService();
        Assert.True(service.TryUpdateAccountSettings(
            account.Id,
            useGlobalSettings: false,
            "https://friend.example/v1/",
            "friend-model",
            "friend-secret",
            clearApiKey: false,
            out _,
            out string errorMessage), errorMessage);

        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler);
        OpenAiCompatibleChatClient client = new(
            httpClient,
            _hostDefaults,
            service);

        await client.CompleteJsonAsync(
            "system",
            "user",
            temperature: 0.2,
            topP: 0.8,
            maximumCompletionTokens: 64,
            aiAccountId: account.Id);

        Assert.Equal(
            "https://friend.example/v1/chat/completions",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("friend-secret", handler.AuthorizationParameter);
        Assert.Contains("\"model\":\"friend-model\"", handler.RequestBody);
    }

    private AiModelConnectionSettingsService CreateService()
    {
        return new AiModelConnectionSettingsService(
            _database.CreateDbContextFactory(),
            _hostDefaults,
            new AiApiKeyProtector());
    }

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}

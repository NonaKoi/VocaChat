using System.Net;
using System.Net.Http.Json;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证模型接口设置经过真实 HTTP 管线保存，同时不会泄露 API Key 原文。
/// </summary>
public sealed class AiModelConnectionSettingsApiTests
{
    [Fact]
    public async Task GlobalAndAccountSettings_CanBeSavedWithoutReturningSecrets()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();

        using HttpResponseMessage globalResponse = await client.PutAsJsonAsync(
            "/api/settings/ai-model",
            new UpdateAiModelConnectionSettingsRequest
            {
                BaseUrl = "https://global.example/v1/",
                Model = "global-model",
                ApiKey = "global-secret"
            });
        string globalJson = await globalResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, globalResponse.StatusCode);
        Assert.DoesNotContain("global-secret", globalJson);
        AiModelConnectionSettingsResponse global =
            (await globalResponse.Content.ReadFromJsonAsync<
                AiModelConnectionSettingsResponse>())!;
        Assert.True(global.HasApiKey);

        AiAccountResponse account = await CreateAccount(client);
        AiAccountModelConnectionSettingsResponse inherited =
            (await client.GetFromJsonAsync<
                AiAccountModelConnectionSettingsResponse>(
                $"/api/ai-accounts/{account.Id}/model-settings"))!;
        Assert.True(inherited.UseGlobalSettings);
        Assert.Equal("global-model", inherited.EffectiveModel);

        using HttpResponseMessage accountResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{account.Id}/model-settings",
            new UpdateAiAccountModelConnectionSettingsRequest
            {
                UseGlobalSettings = false,
                BaseUrl = "https://friend.example/v1/",
                Model = "friend-model",
                ApiKey = "friend-secret"
            });
        string accountJson = await accountResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, accountResponse.StatusCode);
        Assert.DoesNotContain("friend-secret", accountJson);
        AiAccountModelConnectionSettingsResponse dedicated =
            (await accountResponse.Content.ReadFromJsonAsync<
                AiAccountModelConnectionSettingsResponse>())!;
        Assert.False(dedicated.UseGlobalSettings);
        Assert.Equal("friend-model", dedicated.EffectiveModel);
        Assert.True(dedicated.EffectiveHasApiKey);
    }

    private static async Task<AiAccountResponse> CreateAccount(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = "接口设置好友"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AiAccountResponse>())!;
    }
}

using System.Net;
using System.Net.Http.Json;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证好友专有设置通过真实 HTTP 管线保存到临时 SQLite。
/// </summary>
public sealed class AiAccountAutonomySettingsApiTests
{
    [Fact]
    public async Task ExistingAccount_SettingsCanBeUpdatedAndReadAgain()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse account = await CreateAccount(client);

        AiAccountAutonomySettingsResponse defaults =
            (await client.GetFromJsonAsync<AiAccountAutonomySettingsResponse>(
                $"/api/ai-accounts/{account.Id}/autonomy-settings"))!;
        Assert.Equal("Normal", defaults.InitiativeLevel);
        Assert.True(defaults.UseGlobalReplyDelay);
        Assert.True(defaults.UseGlobalConsecutiveMessageDelay);
        Assert.True(defaults.UseGlobalQuestionPolicy);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{account.Id}/autonomy-settings",
            new UpdateAiAccountAutonomySettingsRequest
            {
                IsEnabled = true,
                InitiativeLevel = "High",
                CanInitiatePrivateChats = false,
                CanInitiateGroupChats = true,
                CanJoinGroupChats = false,
                UseGlobalReplyDelay = false,
                ReplyDelayMode = "Fixed",
                FixedReplyDelayMilliseconds = 3400,
                MinimumReplyDelayMilliseconds = 0,
                MaximumReplyDelayMilliseconds = 0,
                UseGlobalConsecutiveMessageDelay = false,
                ConsecutiveMessageDelayMode = "RandomRange",
                FixedConsecutiveMessageDelayMilliseconds = 700,
                MinimumConsecutiveMessageDelayMilliseconds = 250,
                MaximumConsecutiveMessageDelayMilliseconds = 950,
                UseGlobalQuestionPolicy = false,
                MaximumConsecutiveQuestionTurns = 3
            });
        AiAccountAutonomySettingsResponse saved =
            (await updateResponse.Content.ReadFromJsonAsync<
                AiAccountAutonomySettingsResponse>())!;

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(account.Id, saved.AiAccountId);
        Assert.Equal("High", saved.InitiativeLevel);
        Assert.False(saved.CanInitiatePrivateChats);
        Assert.False(saved.UseGlobalReplyDelay);
        Assert.Equal("Fixed", saved.ReplyDelayMode);
        Assert.Equal(3400, saved.FixedReplyDelayMilliseconds);
        Assert.False(saved.UseGlobalConsecutiveMessageDelay);
        Assert.Equal(250, saved.MinimumConsecutiveMessageDelayMilliseconds);
        Assert.Equal(950, saved.MaximumConsecutiveMessageDelayMilliseconds);
        Assert.False(saved.UseGlobalQuestionPolicy);
        Assert.Equal(3, saved.MaximumConsecutiveQuestionTurns);

        AiAccountAutonomySettingsResponse reloaded =
            (await client.GetFromJsonAsync<AiAccountAutonomySettingsResponse>(
                $"/api/ai-accounts/{account.Id}/autonomy-settings"))!;
        Assert.Equal(saved.InitiativeLevel, reloaded.InitiativeLevel);
        Assert.Equal(saved.CanJoinGroupChats, reloaded.CanJoinGroupChats);
        Assert.Equal(
            saved.FixedReplyDelayMilliseconds,
            reloaded.FixedReplyDelayMilliseconds);
        Assert.Equal(
            saved.MaximumConsecutiveMessageDelayMilliseconds,
            reloaded.MaximumConsecutiveMessageDelayMilliseconds);
        Assert.Equal(
            saved.MaximumConsecutiveQuestionTurns,
            reloaded.MaximumConsecutiveQuestionTurns);
    }

    [Fact]
    public async Task MissingAccountAndUnknownInitiativeLevelReturnClearStatuses()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        Guid missingId = Guid.NewGuid();

        using HttpResponseMessage missingResponse = await client.GetAsync(
            $"/api/ai-accounts/{missingId}/autonomy-settings");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        AiAccountResponse account = await CreateAccount(client);
        using HttpResponseMessage invalidResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{account.Id}/autonomy-settings",
            new UpdateAiAccountAutonomySettingsRequest
            {
                IsEnabled = true,
                InitiativeLevel = "Always",
                CanInitiatePrivateChats = true,
                CanInitiateGroupChats = true,
                CanJoinGroupChats = true
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    private static async Task<AiAccountResponse> CreateAccount(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest { Nickname = $"Autonomy-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiAccountResponse>())!;
    }
}

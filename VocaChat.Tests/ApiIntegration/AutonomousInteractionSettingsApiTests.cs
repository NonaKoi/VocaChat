using System.Net;
using System.Net.Http.Json;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证好友自主互动设置通过真实 HTTP 管线保存到临时 SQLite。
/// </summary>
public sealed class AutonomousInteractionSettingsApiTests
{
    [Fact]
    public async Task Settings_CanBeReadUpdatedAndReadAgain()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();

        AutonomousInteractionSettingsResponse defaults =
            (await client.GetFromJsonAsync<AutonomousInteractionSettingsResponse>(
                "/api/settings/autonomous-interactions"))!;

        Assert.False(defaults.IsEnabled);
        Assert.Equal("Normal", defaults.Frequency);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            "/api/settings/autonomous-interactions",
            new UpdateAutonomousInteractionSettingsRequest
            {
                IsEnabled = true,
                Frequency = "High",
                AllowPrivateChats = false,
                AllowGroupChats = true
            });
        AutonomousInteractionSettingsResponse saved =
            (await updateResponse.Content.ReadFromJsonAsync<
                AutonomousInteractionSettingsResponse>())!;

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.True(saved.IsEnabled);
        Assert.Equal("High", saved.Frequency);
        Assert.False(saved.AllowPrivateChats);

        AutonomousInteractionSettingsResponse reloaded =
            (await client.GetFromJsonAsync<AutonomousInteractionSettingsResponse>(
                "/api/settings/autonomous-interactions"))!;

        Assert.Equal(saved.IsEnabled, reloaded.IsEnabled);
        Assert.Equal(saved.Frequency, reloaded.Frequency);
        Assert.Equal(saved.AllowPrivateChats, reloaded.AllowPrivateChats);
        Assert.Equal(saved.AllowGroupChats, reloaded.AllowGroupChats);
    }

    [Fact]
    public async Task Update_WithUnknownFrequency_ReturnsBadRequest()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/settings/autonomous-interactions",
            new UpdateAutonomousInteractionSettingsRequest
            {
                IsEnabled = true,
                Frequency = "EveryMinute",
                AllowPrivateChats = true,
                AllowGroupChats = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

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
        Assert.Equal(80, defaults.PrivateChatContinuationRatePercent);
        Assert.Equal(6, defaults.PrivateChatMaximumRounds);
        Assert.Equal(6, defaults.AutonomousGroupChatMaximumMembers);
        Assert.Equal(80, defaults.GroupChatContinuationRatePercent);
        Assert.Equal(4, defaults.GroupChatMaximumRounds);
        Assert.Equal("RandomRange", defaults.ReplyDelayMode);
        Assert.Equal(800, defaults.MinimumReplyDelayMilliseconds);
        Assert.Equal(1800, defaults.MaximumReplyDelayMilliseconds);
        Assert.Equal("RandomRange", defaults.ConsecutiveMessageDelayMode);
        Assert.Equal(400, defaults.MinimumConsecutiveMessageDelayMilliseconds);
        Assert.Equal(1200, defaults.MaximumConsecutiveMessageDelayMilliseconds);
        Assert.Equal(2, defaults.MaximumConsecutiveQuestionTurns);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            "/api/settings/autonomous-interactions",
            new UpdateAutonomousInteractionSettingsRequest
            {
                IsEnabled = true,
                Frequency = "High",
                AllowPrivateChats = false,
                AllowGroupChats = true,
                PrivateChatContinuationRatePercent = 70,
                PrivateChatMaximumRounds = 8,
                AutonomousGroupChatMaximumMembers = 16,
                GroupChatContinuationRatePercent = 68,
                GroupChatMaximumRounds = 7,
                ReplyDelayMode = "RandomRange",
                FixedReplyDelayMilliseconds = 2500,
                MinimumReplyDelayMilliseconds = 0,
                MaximumReplyDelayMilliseconds = 9_876_543_210,
                ConsecutiveMessageDelayMode = "Fixed",
                FixedConsecutiveMessageDelayMilliseconds = 3456,
                MinimumConsecutiveMessageDelayMilliseconds = 0,
                MaximumConsecutiveMessageDelayMilliseconds = 0,
                MaximumConsecutiveQuestionTurns = 4
            });
        AutonomousInteractionSettingsResponse saved =
            (await updateResponse.Content.ReadFromJsonAsync<
                AutonomousInteractionSettingsResponse>())!;

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.True(saved.IsEnabled);
        Assert.Equal("High", saved.Frequency);
        Assert.False(saved.AllowPrivateChats);
        Assert.Equal(70, saved.PrivateChatContinuationRatePercent);
        Assert.Equal(8, saved.PrivateChatMaximumRounds);
        Assert.Equal(16, saved.AutonomousGroupChatMaximumMembers);
        Assert.Equal(68, saved.GroupChatContinuationRatePercent);
        Assert.Equal(7, saved.GroupChatMaximumRounds);
        Assert.Equal("RandomRange", saved.ReplyDelayMode);
        Assert.Equal(0, saved.MinimumReplyDelayMilliseconds);
        Assert.Equal(9_876_543_210, saved.MaximumReplyDelayMilliseconds);
        Assert.Equal("Fixed", saved.ConsecutiveMessageDelayMode);
        Assert.Equal(3456, saved.FixedConsecutiveMessageDelayMilliseconds);
        Assert.Equal(4, saved.MaximumConsecutiveQuestionTurns);

        AutonomousInteractionSettingsResponse reloaded =
            (await client.GetFromJsonAsync<AutonomousInteractionSettingsResponse>(
                "/api/settings/autonomous-interactions"))!;

        Assert.Equal(saved.IsEnabled, reloaded.IsEnabled);
        Assert.Equal(saved.Frequency, reloaded.Frequency);
        Assert.Equal(saved.AllowPrivateChats, reloaded.AllowPrivateChats);
        Assert.Equal(saved.AllowGroupChats, reloaded.AllowGroupChats);
        Assert.Equal(
            saved.PrivateChatContinuationRatePercent,
            reloaded.PrivateChatContinuationRatePercent);
        Assert.Equal(
            saved.PrivateChatMaximumRounds,
            reloaded.PrivateChatMaximumRounds);
        Assert.Equal(
            saved.AutonomousGroupChatMaximumMembers,
            reloaded.AutonomousGroupChatMaximumMembers);
        Assert.Equal(
            saved.GroupChatContinuationRatePercent,
            reloaded.GroupChatContinuationRatePercent);
        Assert.Equal(
            saved.GroupChatMaximumRounds,
            reloaded.GroupChatMaximumRounds);
        Assert.Equal(saved.ReplyDelayMode, reloaded.ReplyDelayMode);
        Assert.Equal(
            saved.MaximumReplyDelayMilliseconds,
            reloaded.MaximumReplyDelayMilliseconds);
        Assert.Equal(
            saved.FixedConsecutiveMessageDelayMilliseconds,
            reloaded.FixedConsecutiveMessageDelayMilliseconds);
        Assert.Equal(
            saved.MaximumConsecutiveQuestionTurns,
            reloaded.MaximumConsecutiveQuestionTurns);
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
                AllowGroupChats = true,
                PrivateChatContinuationRatePercent = 80,
                PrivateChatMaximumRounds = 6,
                AutonomousGroupChatMaximumMembers = 6
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

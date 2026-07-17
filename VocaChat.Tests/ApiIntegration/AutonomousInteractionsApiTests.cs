using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.AutonomousInteractions;
using VocaChat.WebApi.Dtos.Relationships;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证自主私信判断通过真实 HTTP 管线读取设置与关系，但不会创建会话或消息。
/// </summary>
public sealed class AutonomousInteractionsApiTests
{
    [Fact]
    public async Task EvaluatePrivateChat_WithStrongRelationship_ReturnsApprovedWithoutCreatingConversation()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse first = await CreateAccount(client, "JudgeApiFirst");
        AiAccountResponse second = await CreateAccount(client, "JudgeApiSecond");

        using HttpResponseMessage settingsResponse = await client.PutAsJsonAsync(
            "/api/settings/autonomous-interactions",
            new UpdateAutonomousInteractionSettingsRequest
            {
                IsEnabled = true,
                Frequency = "Normal",
                AllowPrivateChats = true,
                AllowGroupChats = true
            });
        settingsResponse.EnsureSuccessStatusCode();

        using HttpResponseMessage relationshipResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{first.Id}/relationships/{second.Id}",
            new UpdateAiRelationshipRequest
            {
                Familiarity = 100,
                Affinity = 100,
                Trust = 100
            });
        relationshipResponse.EnsureSuccessStatusCode();

        int conversationCountBefore = await GetConversationCount(client);
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/autonomous-interactions/private-chat/evaluate",
            new EvaluateAutonomousPrivateChatRequest
            {
                FirstAiAccountId = first.Id,
                SecondAiAccountId = second.Id
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AutonomousPrivateChatDecisionResponse decision =
            (await response.Content.ReadFromJsonAsync<
                AutonomousPrivateChatDecisionResponse>())!;
        Assert.True(decision.IsApproved);
        Assert.Equal("Approved", decision.Stage);
        Assert.Equal(first.Id, decision.InitiatorAiAccountId);
        Assert.Equal(second.Id, decision.RecipientAiAccountId);
        Assert.Equal(conversationCountBefore, await GetConversationCount(client));
    }

    [Fact]
    public async Task EvaluatePrivateChat_WithInvalidParticipants_ReturnsClearStatuses()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse account = await CreateAccount(client, "JudgeApiInvalid");

        using HttpResponseMessage selfResponse = await client.PostAsJsonAsync(
            "/api/autonomous-interactions/private-chat/evaluate",
            new EvaluateAutonomousPrivateChatRequest
            {
                FirstAiAccountId = account.Id,
                SecondAiAccountId = account.Id
            });
        Assert.Equal(HttpStatusCode.BadRequest, selfResponse.StatusCode);

        using HttpResponseMessage missingResponse = await client.PostAsJsonAsync(
            "/api/autonomous-interactions/private-chat/evaluate",
            new EvaluateAutonomousPrivateChatRequest
            {
                FirstAiAccountId = account.Id,
                SecondAiAccountId = Guid.NewGuid()
            });
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    private static async Task<int> GetConversationCount(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/conversations");
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return document.RootElement.GetArrayLength();
    }

    private static async Task<AiAccountResponse> CreateAccount(
        HttpClient client,
        string prefix)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = $"{prefix}-{Guid.NewGuid().ToString("N")[..12]}"
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiAccountResponse>())!;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.AutonomousInteractions;
using VocaChat.WebApi.Dtos.PrivateChats;
using VocaChat.WebApi.Dtos.Relationships;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证自主私信判断通过真实 HTTP 管线读取设置与关系，但不会创建会话或消息。
/// </summary>
public sealed class AutonomousInteractionsApiTests
{
    [Fact]
    public async Task RunPrivateChat_WithStrongRelationship_PersistsExchangeAndReturnsConversation()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse first = await CreateAccount(client, "RunApiFirst");
        AiAccountResponse second = await CreateAccount(client, "RunApiSecond");
        await EnableAutonomousPrivateChats(client);
        await SetStrongRelationship(client, first.Id, second.Id);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/autonomous-interactions/private-chat/run",
            new RunAutonomousPrivateChatRequest
            {
                FirstAiAccountId = first.Id,
                SecondAiAccountId = second.Id,
                Topic = "HTTP 验收话题"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AutonomousPrivateChatExecutionResponse execution =
            (await response.Content.ReadFromJsonAsync<
                AutonomousPrivateChatExecutionResponse>())!;
        Assert.Equal("Completed", execution.Status);
        Assert.True(execution.Decision.IsApproved);
        Assert.NotNull(execution.PrivateChat);
        Assert.NotNull(execution.Session);
        Assert.Equal("Completed", execution.Session.Status);
        Assert.Equal("ContinuationProbabilityDeclined", execution.Session.EndReason);
        Assert.Equal(1, execution.Session.CompletedRounds);
        Assert.Equal(6, execution.Session.MaximumRounds);
        Assert.Equal(0, execution.Session.ContinuationRatePercent);
        Assert.Equal("HTTP 验收话题", execution.Session.Topic);
        Assert.Equal("FriendPrivateChat", execution.PrivateChat.Category);
        Assert.Equal(2, execution.PrivateChat.Participants.Count);
        Assert.Equal(2, execution.Rounds.Count);
        Assert.False(execution.Rounds[0].IsClosing);
        Assert.True(execution.Rounds[1].IsClosing);
        Assert.NotEmpty(execution.Messages);

        using HttpResponseMessage historyResponse = await client.GetAsync(
            $"/api/private-chats/{execution.PrivateChat.Id}/messages");
        historyResponse.EnsureSuccessStatusCode();
        PrivateMessageResponse[] history =
            (await historyResponse.Content.ReadFromJsonAsync<
                PrivateMessageResponse[]>())!;
        Assert.Equal(execution.Messages.Count, history.Length);
        Assert.Equal(
            execution.Messages.Select(message => message.Id),
            history.Select(message => message.Id));

        using HttpResponseMessage sessionResponse = await client.GetAsync(
            $"/api/autonomous-interactions/private-chat/sessions/{execution.Session.Id}");
        sessionResponse.EnsureSuccessStatusCode();
        AutonomousPrivateChatSessionResponse storedSession =
            (await sessionResponse.Content.ReadFromJsonAsync<
                AutonomousPrivateChatSessionResponse>())!;
        Assert.Equal(execution.Session.Id, storedSession.Id);

        using HttpResponseMessage latestResponse = await client.GetAsync(
            $"/api/autonomous-interactions/private-chat/{execution.PrivateChat.Id}/sessions/latest");
        latestResponse.EnsureSuccessStatusCode();
        AutonomousPrivateChatSessionResponse latestSession =
            (await latestResponse.Content.ReadFromJsonAsync<
                AutonomousPrivateChatSessionResponse>())!;
        Assert.Equal(execution.Session.Id, latestSession.Id);
    }

    [Fact]
    public async Task RunPrivateChat_WhenDecisionIsRejected_DoesNotCreateConversation()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse first = await CreateAccount(client, "RejectApiFirst");
        AiAccountResponse second = await CreateAccount(client, "RejectApiSecond");
        int conversationCountBefore = await GetConversationCount(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/autonomous-interactions/private-chat/run",
            new RunAutonomousPrivateChatRequest
            {
                FirstAiAccountId = first.Id,
                SecondAiAccountId = second.Id
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AutonomousPrivateChatExecutionResponse execution =
            (await response.Content.ReadFromJsonAsync<
                AutonomousPrivateChatExecutionResponse>())!;
        Assert.Equal("DecisionRejected", execution.Status);
        Assert.False(execution.Decision.IsApproved);
        Assert.Null(execution.PrivateChat);
        Assert.Null(execution.Session);
        Assert.Equal(conversationCountBefore, await GetConversationCount(client));
    }

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
                AllowGroupChats = true,
                PrivateChatContinuationRatePercent = 80,
                PrivateChatMaximumRounds = 6,
                AutonomousGroupChatMaximumMembers = 6
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

    [Fact]
    public async Task RunGroupChat_WithStrongGroup_PersistsThreeSpeakersAndReusesConversation()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse[] accounts =
        {
            await CreateAccount(client, "GroupApiFirst"),
            await CreateAccount(client, "GroupApiSecond"),
            await CreateAccount(client, "GroupApiThird")
        };
        await EnableAutonomousGroupChats(client);
        foreach (AiAccountResponse from in accounts)
        {
            foreach (AiAccountResponse to in accounts.Where(item => item.Id != from.Id))
            {
                await SetStrongRelationship(client, from.Id, to.Id);
            }
        }

        using HttpResponseMessage firstResponse = await client.PostAsJsonAsync(
            "/api/autonomous-interactions/group-chat/run",
            new RunAutonomousGroupChatRequest
            {
                ParticipantAiAccountIds = accounts.Select(account => account.Id).ToList(),
                Topic = "HTTP 好友群聊验收"
            });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        AutonomousGroupChatExecutionResponse firstExecution =
            (await firstResponse.Content.ReadFromJsonAsync<
                AutonomousGroupChatExecutionResponse>())!;
        Assert.Equal("Completed", firstExecution.Status);
        Assert.True(firstExecution.GroupChatCreated);
        Assert.NotNull(firstExecution.GroupChat);
        Assert.False(firstExecution.GroupChat.IncludesLocalUser);
        Assert.Equal(3, firstExecution.GroupChat.Members.Count);
        Assert.NotNull(firstExecution.Session);
        Assert.Equal("Completed", firstExecution.Session.Status);
        Assert.Equal(3, firstExecution.Session.ParticipantAiAccountIds.Count);
        Assert.Equal(
            3,
            firstExecution.Messages
                .Select(message => message.SenderAiAccountId)
                .Distinct()
                .Count());

        using HttpResponseMessage historyResponse = await client.GetAsync(
            $"/api/group-chats/{firstExecution.GroupChat.Id}/messages");
        historyResponse.EnsureSuccessStatusCode();
        VocaChat.WebApi.Dtos.GroupMessages.GroupMessageResponse[] history =
            (await historyResponse.Content.ReadFromJsonAsync<
                VocaChat.WebApi.Dtos.GroupMessages.GroupMessageResponse[]>())!;
        Assert.Equal(firstExecution.Messages.Count, history.Length);

        using HttpResponseMessage secondResponse = await client.PostAsJsonAsync(
            "/api/autonomous-interactions/group-chat/run",
            new RunAutonomousGroupChatRequest
            {
                ParticipantAiAccountIds = accounts.Select(account => account.Id).ToList(),
                Topic = "HTTP 好友群聊复用验收"
            });
        secondResponse.EnsureSuccessStatusCode();
        AutonomousGroupChatExecutionResponse secondExecution =
            (await secondResponse.Content.ReadFromJsonAsync<
                AutonomousGroupChatExecutionResponse>())!;
        Assert.False(secondExecution.GroupChatCreated);
        Assert.Equal(firstExecution.GroupChat.Id, secondExecution.GroupChat!.Id);
    }

    private static async Task<int> GetConversationCount(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/conversations");
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return document.RootElement.GetArrayLength();
    }

    private static async Task EnableAutonomousPrivateChats(HttpClient client)
    {
        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/settings/autonomous-interactions",
            new UpdateAutonomousInteractionSettingsRequest
            {
                IsEnabled = true,
                Frequency = "Normal",
                AllowPrivateChats = true,
                AllowGroupChats = false,
                PrivateChatContinuationRatePercent = 0,
                PrivateChatMaximumRounds = 6,
                AutonomousGroupChatMaximumMembers = 6
            });
        response.EnsureSuccessStatusCode();
    }

    private static async Task EnableAutonomousGroupChats(HttpClient client)
    {
        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/settings/autonomous-interactions",
            new UpdateAutonomousInteractionSettingsRequest
            {
                IsEnabled = true,
                Frequency = "High",
                AllowPrivateChats = true,
                AllowGroupChats = true,
                PrivateChatContinuationRatePercent = 80,
                PrivateChatMaximumRounds = 6,
                AutonomousGroupChatMaximumMembers = 6
            });
        response.EnsureSuccessStatusCode();
    }

    private static async Task SetStrongRelationship(
        HttpClient client,
        Guid firstId,
        Guid secondId)
    {
        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{firstId}/relationships/{secondId}",
            new UpdateAiRelationshipRequest
            {
                Familiarity = 100,
                Affinity = 100,
                Trust = 100
            });
        response.EnsureSuccessStatusCode();
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

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.GroupChats;
using VocaChat.WebApi.Dtos.GroupMessages;
using VocaChat.WebApi.Dtos.Settings;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证群消息 HTTP 请求经过协调 Service 和 SQLite 后的完整结果。
/// </summary>
public sealed class GroupMessagesApiTests
{
    [Fact]
    public async Task SendMessage_ReturnsUserAndMemberAiMessagesAndPersistsHistory()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse member = await CreateAccountAsync(client, "MessageAlpha");
        GroupChatResponse groupChat = await CreateGroupChatAsync(
            client,
            "消息测试群",
            member.Id);

        const string content = "HTTP 完整消息流程";
        Guid clientMessageId = Guid.NewGuid();
        using HttpResponseMessage sendResponse = await client.PostAsJsonAsync(
            $"/api/group-chats/{groupChat.Id}/messages",
            new SendGroupMessageRequest
            {
                ClientMessageId = clientMessageId,
                Content = content
            });
        SendGroupMessageResponse? interaction = await sendResponse.Content
            .ReadFromJsonAsync<SendGroupMessageResponse>();

        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        Assert.NotNull(interaction);
        Assert.Equal(clientMessageId, interaction.UserMessage.Id);
        Assert.Equal("User", interaction.UserMessage.SenderType);
        Assert.Null(interaction.UserMessage.SenderAiAccountId);
        Assert.Equal(content, interaction.UserMessage.Content);
        Assert.NotNull(interaction.UserMessage.InteractionBatchId);
        Assert.Null(interaction.UserMessage.ReplyToMessageId);
        GroupMessageResponse aiReply = Assert.Single(interaction.AiReplies);
        Assert.Equal("Complete", interaction.ReplyCompletion);
        Assert.Equal("AiAccount", aiReply.SenderType);
        Assert.Equal(member.Id, aiReply.SenderAiAccountId);
        Assert.Equal(
            interaction.UserMessage.InteractionBatchId,
            aiReply.InteractionBatchId);
        Assert.Equal(interaction.UserMessage.Id, aiReply.ReplyToMessageId);

        List<GroupMessageResponse>? history = await client
            .GetFromJsonAsync<List<GroupMessageResponse>>(
                $"/api/group-chats/{groupChat.Id}/messages");

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal(interaction.UserMessage.Id, history[0].Id);
        Assert.Equal(aiReply.Id, history[1].Id);
        Assert.Equal(
            interaction.UserMessage.InteractionBatchId,
            history[1].InteractionBatchId);
        Assert.Equal(history[0].Id, history[1].ReplyToMessageId);
        Assert.True(history[0].SentAt <= history[1].SentAt);
    }

    [Fact]
    public async Task SendMessage_WithMemberMention_SelectsMentionedAi()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse firstMember = await CreateAccountAsync(
            client,
            "MentionAlpha");
        AiAccountResponse secondMember = await CreateAccountAsync(
            client,
            "MentionBeta");
        GroupChatResponse groupChat = await CreateGroupChatAsync(
            client,
            "点名测试群",
            firstMember.Id,
            secondMember.Id);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/group-chats/{groupChat.Id}/messages",
            new SendGroupMessageRequest
            {
                Content = $"@{secondMember.Nickname} 请回复"
            });
        SendGroupMessageResponse? interaction = await response.Content
            .ReadFromJsonAsync<SendGroupMessageResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(interaction);
        Assert.Equal(
            secondMember.Id,
            Assert.Single(interaction.AiReplies).SenderAiAccountId);
    }

    [Fact]
    public async Task SendMessage_WithTwoMemberMentions_ReturnsTwoRepliesInMentionOrder()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse firstMember = await CreateAccountAsync(
            client,
            "DoubleMentionAlpha");
        AiAccountResponse secondMember = await CreateAccountAsync(
            client,
            "DoubleMentionBeta");
        GroupChatResponse groupChat = await CreateGroupChatAsync(
            client,
            "双点名测试群",
            firstMember.Id,
            secondMember.Id);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/group-chats/{groupChat.Id}/messages",
            new SendGroupMessageRequest
            {
                Content = $"@{secondMember.Nickname} 先说，@{firstMember.Nickname} 补充"
            });
        SendGroupMessageResponse? interaction = await response.Content
            .ReadFromJsonAsync<SendGroupMessageResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(interaction);
        Assert.Equal("Complete", interaction.ReplyCompletion);
        Assert.Equal(2, interaction.AiReplies.Count);
        Assert.Equal(
            secondMember.Id,
            interaction.AiReplies[0].SenderAiAccountId);
        Assert.Equal(
            firstMember.Id,
            interaction.AiReplies[1].SenderAiAccountId);
        Assert.All(interaction.AiReplies, reply => Assert.Equal(
            interaction.UserMessage.InteractionBatchId,
            reply.InteractionBatchId));
        Assert.All(interaction.AiReplies, reply => Assert.Equal(
            interaction.UserMessage.Id,
            reply.ReplyToMessageId));
    }

    [Fact]
    public async Task GetHistory_ReturnsRecordedTokenUsageForAiMessage()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse member = await CreateAccountAsync(
            client,
            "TokenUsageAlpha");
        GroupChatResponse groupChat = await CreateGroupChatAsync(
            client,
            "Token 用量测试群",
            member.Id);

        using HttpResponseMessage sendResponse = await client.PostAsJsonAsync(
            $"/api/group-chats/{groupChat.Id}/messages",
            new SendGroupMessageRequest { Content = "记录实际 Token" });
        SendGroupMessageResponse? interaction = await sendResponse.Content
            .ReadFromJsonAsync<SendGroupMessageResponse>();
        Assert.NotNull(interaction);
        GroupMessageResponse aiReply = Assert.Single(interaction.AiReplies);

        using IServiceScope scope = factory.Services.CreateScope();
        VocaChatDbContextFactory dbContextFactory = scope.ServiceProvider
            .GetRequiredService<VocaChatDbContextFactory>();
        using VocaChatDbContext dbContext =
            dbContextFactory.CreateDbContext();
        GroupMessage storedReply = dbContext.GroupMessages
            .AsNoTracking()
            .Single(message => message.Id == aiReply.Id);
        Assert.NotNull(storedReply.AiResponseBatchId);
        Assert.NotNull(storedReply.InteractionBatchId);

        AiModelInvocationUsageService usageService = scope.ServiceProvider
            .GetRequiredService<AiModelInvocationUsageService>();
        Assert.True(usageService.TryRecord(
            new AiModelInvocationContext
            {
                Stage = AiModelInvocationStage.GroupDirector,
                GroupChatId = groupChat.Id,
                InteractionBatchId = storedReply.InteractionBatchId
            },
            "director-model",
            new AiModelTokenUsage(90, 10, 100, null, null, null)));
        Assert.True(usageService.TryRecord(
            new AiModelInvocationContext
            {
                Stage = AiModelInvocationStage.ReplyGeneration,
                AiAccountId = member.Id,
                GroupChatId = groupChat.Id,
                InteractionBatchId = storedReply.InteractionBatchId,
                AiResponseBatchId = storedReply.AiResponseBatchId
            },
            "reply-model",
            new AiModelTokenUsage(120, 30, 150, 50, 70, 5)));

        List<GroupMessageResponse>? history = await client
            .GetFromJsonAsync<List<GroupMessageResponse>>(
                $"/api/group-chats/{groupChat.Id}/messages");
        GroupMessageResponse reloadedReply = Assert.Single(
            Assert.IsType<List<GroupMessageResponse>>(history),
            message => message.Id == aiReply.Id);

        Assert.NotNull(reloadedReply.TokenUsage);
        Assert.Equal(250, reloadedReply.TokenUsage.TotalTokens);
        Assert.Equal(
            100,
            reloadedReply.TokenUsage.GroupDirector?.TotalTokens);
        Assert.Equal(
            150,
            reloadedReply.TokenUsage.ReplyGeneration?.TotalTokens);
        Assert.Equal(
            50,
            reloadedReply.TokenUsage.ReplyGeneration?.CacheHitTokens);
    }

    [Fact]
    public async Task SendMessage_WithBlankContent_ReturnsBadRequestWithoutMessages()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse member = await CreateAccountAsync(client, "BlankAlpha");
        GroupChatResponse groupChat = await CreateGroupChatAsync(
            client,
            "空白消息测试群",
            member.Id);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/group-chats/{groupChat.Id}/messages",
            new SendGroupMessageRequest { Content = "   " });
        List<GroupMessageResponse>? history = await client
            .GetFromJsonAsync<List<GroupMessageResponse>>(
                $"/api/group-chats/{groupChat.Id}/messages");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task SendMessage_WhenAiReplyFails_ReturnsSavedUserMessageAndKeepsIt()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse member = await CreateAccountAsync(client, "PartialAlpha");
        GroupChatResponse groupChat = await CreateGroupChatAsync(
            client,
            "部分成功测试群",
            member.Id);
        string maximumLengthContent = new('a', GroupMessage.ContentMaxLength);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/group-chats/{groupChat.Id}/messages",
            new SendGroupMessageRequest { Content = maximumLengthContent });
        SendGroupMessageFailureResponse? failure = await response.Content
            .ReadFromJsonAsync<SendGroupMessageFailureResponse>();
        List<GroupMessageResponse>? history = await client
            .GetFromJsonAsync<List<GroupMessageResponse>>(
                $"/api/group-chats/{groupChat.Id}/messages");

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);
        Assert.NotNull(failure);
        Assert.Equal(
            "群聊回复暂时生成失败，已保留你发送的消息。",
            failure.Message);
        Assert.NotNull(failure.SavedUserMessage);
        Assert.Empty(failure.SavedAiReplies);
        Assert.Equal(maximumLengthContent, failure.SavedUserMessage.Content);
        Assert.NotNull(history);
        GroupMessageResponse storedMessage = Assert.Single(history);
        Assert.Equal(failure.SavedUserMessage.Id, storedMessage.Id);
        Assert.Equal("User", storedMessage.SenderType);

        List<AiInteractionDiagnosticLogResponse>? logs = await client
            .GetFromJsonAsync<List<AiInteractionDiagnosticLogResponse>>(
                "/api/settings/interaction-logs?limit=10");
        Assert.NotNull(logs);
        AiInteractionDiagnosticLogResponse log = Assert.Single(
            logs,
            item => item.Code == "GroupConversationExecutionFailed");
        Assert.Equal("GroupPrimaryReply", log.Scenario);
        Assert.Equal(groupChat.Id, log.ConversationId);
        Assert.False(log.WasRecovered);
        Assert.Contains(
            logs,
            item => item.Code == "GroupConversationPlanFallback");
    }

    private static async Task<AiAccountResponse> CreateAccountAsync(
        HttpClient client,
        string nickname)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest { Nickname = nickname });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AiAccountResponse? account = await response.Content
            .ReadFromJsonAsync<AiAccountResponse>();
        return Assert.IsType<AiAccountResponse>(account);
    }

    private static async Task<GroupChatResponse> CreateGroupChatAsync(
        HttpClient client,
        string name,
        params Guid[] memberIds)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/group-chats",
            new CreateGroupChatRequest
            {
                Name = name,
                MemberAiAccountIds = memberIds.ToList()
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        GroupChatResponse? groupChat = await response.Content
            .ReadFromJsonAsync<GroupChatResponse>();
        return Assert.IsType<GroupChatResponse>(groupChat);
    }
}

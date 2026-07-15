using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using VocaChat.Models;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.GroupChats;
using VocaChat.WebApi.Dtos.GroupMessages;

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
        using HttpResponseMessage sendResponse = await client.PostAsJsonAsync(
            $"/api/group-chats/{groupChat.Id}/messages",
            new SendGroupMessageRequest { Content = content });
        SendGroupMessageResponse? interaction = await sendResponse.Content
            .ReadFromJsonAsync<SendGroupMessageResponse>();

        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        Assert.NotNull(interaction);
        Assert.Equal("User", interaction.UserMessage.SenderType);
        Assert.Null(interaction.UserMessage.SenderAiAccountId);
        Assert.Equal(content, interaction.UserMessage.Content);
        Assert.Equal("AiAccount", interaction.AiReply.SenderType);
        Assert.Equal(member.Id, interaction.AiReply.SenderAiAccountId);

        List<GroupMessageResponse>? history = await client
            .GetFromJsonAsync<List<GroupMessageResponse>>(
                $"/api/group-chats/{groupChat.Id}/messages");

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal(interaction.UserMessage.Id, history[0].Id);
        Assert.Equal(interaction.AiReply.Id, history[1].Id);
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
        Assert.Equal(secondMember.Id, interaction.AiReply.SenderAiAccountId);
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
        Assert.NotNull(failure.SavedUserMessage);
        Assert.Equal(maximumLengthContent, failure.SavedUserMessage.Content);
        Assert.NotNull(history);
        GroupMessageResponse storedMessage = Assert.Single(history);
        Assert.Equal(failure.SavedUserMessage.Id, storedMessage.Id);
        Assert.Equal("User", storedMessage.SenderType);
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

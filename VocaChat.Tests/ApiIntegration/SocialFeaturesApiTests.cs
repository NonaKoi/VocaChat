using System.Net;
using System.Net.Http.Json;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.Contacts;
using VocaChat.WebApi.Dtos.Posts;
using VocaChat.WebApi.Dtos.PrivateChats;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证账号成为好友后能够贯通私聊和动态的真实 HTTP 管线。
/// </summary>
public sealed class SocialFeaturesApiTests
{
    [Fact]
    public async Task CreatedFriend_CanPrivateChatAndPublishInteractivePost()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();

        using HttpResponseMessage accountResponse = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest { Nickname = "SocialApiFriend" });
        AiAccountResponse account = (await accountResponse.Content.ReadFromJsonAsync<AiAccountResponse>())!;
        ContactResponse contact = Assert.Single(
            (await client.GetFromJsonAsync<ContactResponse[]>("/api/contacts"))!);
        Assert.Equal(account.Id, contact.Friend.Id);

        using HttpResponseMessage chatResponse = await client.PutAsJsonAsync(
            $"/api/contacts/{contact.Id}/private-chat",
            new { });
        PrivateChatResponse chat = (await chatResponse.Content.ReadFromJsonAsync<PrivateChatResponse>())!;
        using HttpResponseMessage messageResponse = await client.PostAsJsonAsync(
            $"/api/private-chats/{chat.Id}/messages",
            new SendPrivateMessageRequest { Content = "你好，朋友。" });
        SendPrivateMessageResponse interaction = (await messageResponse.Content.ReadFromJsonAsync<SendPrivateMessageResponse>())!;
        Assert.Equal(HttpStatusCode.OK, messageResponse.StatusCode);
        Assert.Equal(account.Id, interaction.AiReply.SenderAiAccountId);

        using HttpResponseMessage postResponse = await client.PostAsJsonAsync(
            "/api/posts",
            new CreatePostRequest { AuthorAiAccountId = account.Id, Content = "第一条动态" });
        PostResponse post = (await postResponse.Content.ReadFromJsonAsync<PostResponse>())!;
        using HttpResponseMessage likeResponse = await client.PutAsJsonAsync(
            $"/api/posts/{post.Id}/like",
            new { });
        using HttpResponseMessage commentResponse = await client.PostAsJsonAsync(
            $"/api/posts/{post.Id}/comments",
            new CreatePostCommentRequest { Content = "欢迎！" });
        PostResponse updated = (await commentResponse.Content.ReadFromJsonAsync<PostResponse>())!;

        Assert.Equal(HttpStatusCode.OK, likeResponse.StatusCode);
        Assert.True(updated.IsLikedByLocalUser);
        Assert.Equal(1, updated.CommentCount);
    }
}

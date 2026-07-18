using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.GroupChats;
using VocaChat.WebApi.Dtos.GroupMessages;
using VocaChat.WebApi.Services;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证账号媒体经过 multipart、文件系统、SQLite 和响应 DTO 的完整链路。
/// </summary>
public sealed class AiAccountMediaApiTests
{
    private static readonly byte[] PngImage = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task UploadAndReplaceMedia_PersistsIdentifiersAndCleansTemporaryDirectory()
    {
        VocaChatWebApiFactory factory = new();
        string mediaDirectory = factory.MediaDirectory;

        try
        {
            using HttpClient client = factory.CreateApiClient();
            AiAccountResponse account = await CreateAccountAsync(
                client,
                "MediaHttpAlpha");

            AiAccountResponse firstAvatar = await UploadImageAsync(
                client,
                $"/api/ai-accounts/{account.Id}/avatar",
                "avatar.png",
                PngImage);
            string firstAvatarUrl = Assert.IsType<string>(firstAvatar.AvatarUrl);

            using HttpResponseMessage avatarReadResponse = await client.GetAsync(
                firstAvatarUrl);
            Assert.Equal(HttpStatusCode.OK, avatarReadResponse.StatusCode);
            Assert.Equal("image/png", avatarReadResponse.Content.Headers.ContentType?.MediaType);
            Assert.Equal(PngImage, await avatarReadResponse.Content.ReadAsByteArrayAsync());

            AiAccountResponse secondAvatar = await UploadImageAsync(
                client,
                $"/api/ai-accounts/{account.Id}/avatar",
                "avatar-replacement.png",
                PngImage);
            Assert.NotEqual(firstAvatar.AvatarUrl, secondAvatar.AvatarUrl);
            Assert.Single(Directory.EnumerateFiles(
                mediaDirectory,
                "*",
                SearchOption.AllDirectories));

            AiAccountResponse cover = await UploadImageAsync(
                client,
                $"/api/ai-accounts/{account.Id}/cover",
                "cover.png",
                PngImage);
            string coverUrl = Assert.IsType<string>(cover.CoverUrl);

            using HttpResponseMessage coverReadResponse = await client.GetAsync(
                coverUrl);
            Assert.Equal(HttpStatusCode.OK, coverReadResponse.StatusCode);
            Assert.Equal(PngImage, await coverReadResponse.Content.ReadAsByteArrayAsync());

            using VocaChatDbContext dbContext = new VocaChatDbContextFactory(
                $"Data Source={factory.DatabasePath};Foreign Keys=True;Pooling=False")
                .CreateDbContext();
            AiAccount storedAccount = await dbContext.AiAccounts
                .SingleAsync(item => item.Id == account.Id);

            Assert.False(Path.IsPathRooted(storedAccount.AvatarMediaId));
            Assert.False(Path.IsPathRooted(storedAccount.ProfileCoverMediaId));
            Assert.Equal(2, Directory.EnumerateFiles(
                mediaDirectory,
                "*",
                SearchOption.AllDirectories).Count());
        }
        finally
        {
            factory.Dispose();
        }

        Assert.False(Directory.Exists(mediaDirectory));
    }

    [Fact]
    public async Task UploadAvatar_RejectsInvalidOversizedAndMissingAccountRequests()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse account = await CreateAccountAsync(
            client,
            "MediaHttpBeta");

        using HttpResponseMessage unsupportedResponse = await PutImageAsync(
            client,
            $"/api/ai-accounts/{account.Id}/avatar",
            "avatar.txt",
            "text/plain",
            "not-an-image"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, unsupportedResponse.StatusCode);

        byte[] oversizedImage = new byte[checked((int)
            (AiAccountMediaService.AvatarMaximumLength + 1))];
        PngImage.AsSpan(0, 8).CopyTo(oversizedImage);
        using HttpResponseMessage oversizedResponse = await PutImageAsync(
            client,
            $"/api/ai-accounts/{account.Id}/avatar",
            "large.png",
            "image/png",
            oversizedImage);
        Assert.Equal(
            HttpStatusCode.RequestEntityTooLarge,
            oversizedResponse.StatusCode);

        using HttpResponseMessage missingAccountResponse = await PutImageAsync(
            client,
            $"/api/ai-accounts/{Guid.NewGuid()}/avatar",
            "avatar.png",
            "image/png",
            PngImage);
        Assert.Equal(HttpStatusCode.NotFound, missingAccountResponse.StatusCode);

        using HttpResponseMessage missingMediaResponse = await client.GetAsync(
            $"/api/ai-accounts/{account.Id}/cover");
        Assert.Equal(HttpStatusCode.NotFound, missingMediaResponse.StatusCode);
    }

    [Fact]
    public async Task AvatarUrl_IsReusedByGroupMembersAndAiMessages()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse account = await CreateAccountAsync(
            client,
            "MediaHttpGamma");
        AiAccountResponse accountWithAvatar = await UploadImageAsync(
            client,
            $"/api/ai-accounts/{account.Id}/avatar",
            "avatar.png",
            PngImage);

        using HttpResponseMessage groupCreateResponse = await client.PostAsJsonAsync(
            "/api/group-chats",
            new CreateGroupChatRequest
            {
                Name = "媒体同步群",
                MemberAiAccountIds = new List<Guid> { account.Id }
            });
        GroupChatResponse? groupChat = await groupCreateResponse.Content
            .ReadFromJsonAsync<GroupChatResponse>();

        Assert.Equal(HttpStatusCode.Created, groupCreateResponse.StatusCode);
        Assert.NotNull(groupChat);
        Assert.Equal(
            accountWithAvatar.AvatarUrl,
            Assert.Single(groupChat.Members).AvatarUrl);

        using HttpResponseMessage sendResponse = await client.PostAsJsonAsync(
            $"/api/group-chats/{groupChat.Id}/messages",
            new SendGroupMessageRequest { Content = "检查头像同步" });
        SendGroupMessageResponse? interaction = await sendResponse.Content
            .ReadFromJsonAsync<SendGroupMessageResponse>();

        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        Assert.NotNull(interaction);
        Assert.Null(interaction.UserMessage.SenderAvatarUrl);
        Assert.Equal(
            accountWithAvatar.AvatarUrl,
            Assert.Single(interaction.AiReplies).SenderAvatarUrl);
    }

    private static async Task<AiAccountResponse> CreateAccountAsync(
        HttpClient client,
        string nickname)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest { Nickname = nickname });
        AiAccountResponse? account = await response.Content
            .ReadFromJsonAsync<AiAccountResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<AiAccountResponse>(account);
    }

    private static async Task<AiAccountResponse> UploadImageAsync(
        HttpClient client,
        string path,
        string fileName,
        byte[] image)
    {
        using HttpResponseMessage response = await PutImageAsync(
            client,
            path,
            fileName,
            "image/png",
            image);
        AiAccountResponse? account = await response.Content
            .ReadFromJsonAsync<AiAccountResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<AiAccountResponse>(account);
    }

    private static async Task<HttpResponseMessage> PutImageAsync(
        HttpClient client,
        string path,
        string fileName,
        string contentType,
        byte[] image)
    {
        using MultipartFormDataContent form = new();
        using ByteArrayContent fileContent = new(image);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        return await client.PutAsync(path, form);
    }
}

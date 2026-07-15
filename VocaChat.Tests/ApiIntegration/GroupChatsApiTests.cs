using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.GroupChats;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证群聊创建、Location 查询和群成员增量添加的完整 HTTP 生命周期。
/// </summary>
public sealed class GroupChatsApiTests
{
    [Fact]
    public async Task CreateGroupAndAddMember_PersistsRelationsWithoutDuplicatingAccounts()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse firstAccount = await CreateAccountAsync(
            client,
            "GroupAlpha");
        AiAccountResponse secondAccount = await CreateAccountAsync(
            client,
            "GroupBeta");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/group-chats",
            new CreateGroupChatRequest
            {
                Name = "HTTP 群聊",
                MemberAiAccountIds = new List<Guid> { firstAccount.Id }
            });
        GroupChatResponse? createdGroup = await createResponse.Content
            .ReadFromJsonAsync<GroupChatResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        Assert.NotNull(createdGroup);
        Assert.Equal(firstAccount.Id, Assert.Single(createdGroup.Members).Id);

        using HttpResponseMessage initialGetResponse = await client.GetAsync(
            createResponse.Headers.Location);
        string initialGroupJson = await initialGetResponse.Content
            .ReadAsStringAsync();
        GroupChatResponse? initialGroup = JsonSerializer.Deserialize<GroupChatResponse>(
            initialGroupJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, initialGetResponse.StatusCode);
        Assert.NotNull(initialGroup);
        Assert.Single(initialGroup.Members);
        AssertMemberContainsOnlySummaryFields(initialGroupJson);

        string addMemberPath = $"/api/group-chats/{createdGroup.Id}/members";
        AddGroupChatMemberRequest addMemberRequest = new()
        {
            AiAccountId = secondAccount.Id
        };
        using HttpResponseMessage addMemberResponse = await client.PostAsJsonAsync(
            addMemberPath,
            addMemberRequest);
        GroupChatResponse? updatedGroup = await addMemberResponse.Content
            .ReadFromJsonAsync<GroupChatResponse>();

        Assert.Equal(HttpStatusCode.OK, addMemberResponse.StatusCode);
        Assert.NotNull(updatedGroup);
        Assert.Equal(2, updatedGroup.Members.Count);
        Assert.Contains(updatedGroup.Members, member => member.Id == firstAccount.Id);
        Assert.Contains(updatedGroup.Members, member => member.Id == secondAccount.Id);

        using HttpResponseMessage duplicateMemberResponse =
            await client.PostAsJsonAsync(addMemberPath, addMemberRequest);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateMemberResponse.StatusCode);

        GroupChatResponse? reloadedGroup = await client.GetFromJsonAsync<GroupChatResponse>(
            $"/api/group-chats/{createdGroup.Id}");
        List<AiAccountResponse>? storedAccounts = await client
            .GetFromJsonAsync<List<AiAccountResponse>>("/api/ai-accounts");

        Assert.NotNull(reloadedGroup);
        Assert.Equal(2, reloadedGroup.Members.Count);
        Assert.NotNull(storedAccounts);
        Assert.Equal(2, storedAccounts.Count);
    }

    private static async Task<AiAccountResponse> CreateAccountAsync(
        HttpClient client,
        string nickname)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = nickname
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AiAccountResponse? account = await response.Content
            .ReadFromJsonAsync<AiAccountResponse>();
        return Assert.IsType<AiAccountResponse>(account);
    }

    /// <summary>
    /// 检查群成员 HTTP 摘要没有泄露完整 AI 账号字段。
    /// </summary>
    private static void AssertMemberContainsOnlySummaryFields(string groupJson)
    {
        using JsonDocument document = JsonDocument.Parse(groupJson);
        JsonElement member = document.RootElement
            .GetProperty("members")[0];
        string[] propertyNames = member
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(new[] { "id", "nickname" }, propertyNames);
    }
}

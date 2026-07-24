using System.Net;
using System.Net.Http.Json;
using VocaChat.Models;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.CharacterWorlds;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证角色世界 HTTP 契约及账号共享世界的完整持久化链路。
/// </summary>
public sealed class CharacterWorldsApiTests
{
    [Fact]
    public async Task CreateUpdateAndAssignWorld_UsesSharedPersistentEntity()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();

        IReadOnlyList<CharacterWorldResponse>? initialWorlds =
            await client.GetFromJsonAsync<
                IReadOnlyList<CharacterWorldResponse>>(
                "/api/character-worlds");
        CharacterWorldResponse defaultWorld =
            Assert.Single(Assert.IsAssignableFrom<
                IReadOnlyList<CharacterWorldResponse>>(initialWorlds));
        Assert.Equal(CharacterWorld.DefaultWorldId, defaultWorld.Id);

        using HttpResponseMessage createWorldResponse =
            await client.PostAsJsonAsync(
                "/api/character-worlds",
                new CreateCharacterWorldRequest
                {
                    Name = "蔚蓝档案世界",
                    Description = "以用户填写的学园都市设定为最高依据。"
                });
        CharacterWorldResponse? createdWorld =
            await createWorldResponse.Content
                .ReadFromJsonAsync<CharacterWorldResponse>();

        Assert.Equal(HttpStatusCode.Created, createWorldResponse.StatusCode);
        Assert.NotNull(createWorldResponse.Headers.Location);
        Assert.NotNull(createdWorld);

        AiAccountResponse first = await CreateAccount(
            client,
            "世界测试好友一",
            createdWorld.Id);
        AiAccountResponse second = await CreateAccount(
            client,
            "世界测试好友二",
            createdWorld.Id);

        Assert.Equal(createdWorld.Id, first.CharacterWorldId);
        Assert.Equal(createdWorld.Id, second.CharacterWorldId);
        Assert.Equal("蔚蓝档案世界", first.CharacterWorld.Name);

        using HttpResponseMessage updateWorldResponse =
            await client.PutAsJsonAsync(
                $"/api/character-worlds/{createdWorld.Id}",
                new UpdateCharacterWorldRequest
                {
                    Name = "基沃托斯",
                    Description = "用户说明优先于模型已有知识。"
                });
        CharacterWorldResponse? updatedWorld =
            await updateWorldResponse.Content
                .ReadFromJsonAsync<CharacterWorldResponse>();

        Assert.Equal(HttpStatusCode.OK, updateWorldResponse.StatusCode);
        Assert.NotNull(updatedWorld);
        Assert.Equal("基沃托斯", updatedWorld.Name);

        AiAccountResponse? reloadedFirst =
            await client.GetFromJsonAsync<AiAccountResponse>(
                $"/api/ai-accounts/{first.Id}");
        Assert.NotNull(reloadedFirst);
        Assert.Equal(createdWorld.Id, reloadedFirst.CharacterWorldId);
        Assert.Equal("基沃托斯", reloadedFirst.CharacterWorld.Name);

        CharacterWorldResponse? storedWorld =
            await client.GetFromJsonAsync<CharacterWorldResponse>(
                createWorldResponse.Headers.Location);
        Assert.NotNull(storedWorld);
        Assert.Equal("用户说明优先于模型已有知识。", storedWorld.Description);
    }

    [Fact]
    public async Task DuplicateOrMissingWorld_ReturnsBusinessFailure()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();

        using HttpResponseMessage duplicateWorldResponse =
            await client.PostAsJsonAsync(
                "/api/character-worlds",
                new CreateCharacterWorldRequest
                {
                    Name = CharacterWorld.DefaultWorldName
                });
        Assert.Equal(
            HttpStatusCode.BadRequest,
            duplicateWorldResponse.StatusCode);

        using HttpResponseMessage missingWorldAccountResponse =
            await client.PostAsJsonAsync(
                "/api/ai-accounts",
                new CreateAiAccountRequest
                {
                    Nickname = "不存在世界的好友",
                    CharacterWorldId = Guid.NewGuid()
                });
        Assert.Equal(
            HttpStatusCode.BadRequest,
            missingWorldAccountResponse.StatusCode);
    }

    private static async Task<AiAccountResponse> CreateAccount(
        HttpClient client,
        string nickname,
        Guid characterWorldId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = nickname,
                CharacterWorldId = characterWorldId
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<AiAccountResponse>())!;
    }
}

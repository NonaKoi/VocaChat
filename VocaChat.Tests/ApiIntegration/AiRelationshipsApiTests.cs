using System.Net;
using System.Net.Http.Json;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.Relationships;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证好友关系通过真实 HTTP 管线读取并保存到临时 SQLite。
/// </summary>
public sealed class AiRelationshipsApiTests
{
    [Fact]
    public async Task DirectionalRelationship_CanBeUpdatedAndReadAgain()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse first = await CreateAccount(client, "RelationApiFirst");
        AiAccountResponse second = await CreateAccount(client, "RelationApiSecond");

        AiRelationshipResponse defaults =
            (await client.GetFromJsonAsync<AiRelationshipResponse>(
                $"/api/ai-accounts/{first.Id}/relationships/{second.Id}"))!;
        Assert.Equal(10, defaults.Familiarity);
        Assert.Null(defaults.UpdatedAt);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{first.Id}/relationships/{second.Id}",
            new UpdateAiRelationshipRequest
            {
                Familiarity = 65,
                Affinity = 25,
                Trust = 50
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        AiRelationshipResponse saved =
            (await updateResponse.Content.ReadFromJsonAsync<AiRelationshipResponse>())!;
        Assert.Equal(65, saved.Familiarity);
        Assert.NotNull(saved.UpdatedAt);

        AiRelationshipResponse reloaded =
            (await client.GetFromJsonAsync<AiRelationshipResponse>(
                $"/api/ai-accounts/{first.Id}/relationships/{second.Id}"))!;
        AiRelationshipResponse reverse =
            (await client.GetFromJsonAsync<AiRelationshipResponse>(
                $"/api/ai-accounts/{second.Id}/relationships/{first.Id}"))!;
        Assert.Equal(65, reloaded.Familiarity);
        Assert.Equal(10, reverse.Familiarity);
    }

    [Fact]
    public async Task InvalidRelationshipRequests_ReturnClearStatuses()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse account = await CreateAccount(client, "RelationApiInvalid");

        using HttpResponseMessage selfResponse = await client.GetAsync(
            $"/api/ai-accounts/{account.Id}/relationships/{account.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, selfResponse.StatusCode);

        using HttpResponseMessage missingResponse = await client.GetAsync(
            $"/api/ai-accounts/{account.Id}/relationships/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        using HttpResponseMessage invalidValueResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{account.Id}/relationships/{Guid.NewGuid()}",
            new UpdateAiRelationshipRequest
            {
                Familiarity = 101,
                Affinity = 0,
                Trust = 10
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidValueResponse.StatusCode);
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

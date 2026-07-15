using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using VocaChat.WebApi.Dtos.AiAccounts;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证 AI 账号 HTTP 创建、Location 查询和重复昵称失败链路。
/// </summary>
public sealed class AiAccountsApiTests
{
    [Fact]
    public async Task CreateAndGetAiAccount_UsesHttpPipelineAndPersistsData()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        CreateAiAccountRequest request = new()
        {
            Nickname = "ApiAlpha",
            IdentityDescription = "HTTP 测试账号",
            Personality = "冷静",
            SpeakingStyle = "简洁"
        };

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            request);
        AiAccountResponse? createdAccount = await createResponse.Content
            .ReadFromJsonAsync<AiAccountResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        Assert.NotNull(createdAccount);
        Assert.Equal(request.Nickname, createdAccount.Nickname);

        using HttpResponseMessage getResponse = await client.GetAsync(
            createResponse.Headers.Location);
        AiAccountResponse? storedAccount = await getResponse.Content
            .ReadFromJsonAsync<AiAccountResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(storedAccount);
        Assert.Equal(createdAccount.Id, storedAccount.Id);
        Assert.Equal(createdAccount.Nickname, storedAccount.Nickname);

        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = "apialpha"
            });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }
}

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
            VcNumber = "Api#Alpha!",
            IdentityDescription = "HTTP 测试账号",
            Personality = "冷静",
            SpeakingStyle = "简洁",
            Signature = "按自己的节奏前进。",
            Birthday = new DateOnly(2000, 7, 23),
            Gender = "Male",
            Location = "中国 上海",
            Occupation = "插画师",
            Hometown = "中国 杭州",
            OnlineStatus = "Online",
            InterestTags = new[] { "绘画", "阅读" },
            PersonalityTags = new[] { "冷静", "理性" }
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
        Assert.Equal(request.VcNumber, createdAccount.VcNumber);
        Assert.Equal(request.Signature, createdAccount.Signature);
        Assert.Equal(request.Birthday, createdAccount.Birthday);
        Assert.Equal("Male", createdAccount.Gender);
        Assert.Equal("Online", createdAccount.OnlineStatus);
        Assert.Equal("狮子座", createdAccount.ZodiacSign);
        Assert.Equal(request.InterestTags, createdAccount.InterestTags);
        Assert.Equal(request.PersonalityTags, createdAccount.PersonalityTags);

        using HttpResponseMessage getResponse = await client.GetAsync(
            createResponse.Headers.Location);
        AiAccountResponse? storedAccount = await getResponse.Content
            .ReadFromJsonAsync<AiAccountResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(storedAccount);
        Assert.Equal(createdAccount.Id, storedAccount.Id);
        Assert.Equal(createdAccount.VcNumber, storedAccount.VcNumber);
        Assert.Equal(createdAccount.Nickname, storedAccount.Nickname);
        Assert.Equal(createdAccount.Signature, storedAccount.Signature);
        Assert.Equal(createdAccount.InterestTags, storedAccount.InterestTags);

        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = "apialpha"
            });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }
}

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

    [Fact]
    public async Task UpdateAiAccount_ReturnsUpdatedProfileAndExplicitErrors()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse target = await CreateAccount(
            client,
            "UpdateTarget",
            "Update#Target");
        AiAccountResponse existing = await CreateAccount(
            client,
            "ExistingAccount",
            "Existing#01");
        UpdateAiAccountRequest request = new()
        {
            Nickname = "  更新后的好友  ",
            VcNumber = "  Updated#01  ",
            IdentityDescription = "  正在完善个人作品集的插画师  ",
            Personality = "温和、认真",
            SpeakingStyle = "自然简洁",
            Signature = "慢慢把想法画下来。",
            Birthday = new DateOnly(1998, 2, 14),
            Gender = "Female",
            Location = "中国 南京",
            Occupation = "插画师",
            Hometown = "中国 苏州",
            OnlineStatus = "Away",
            InterestTags = new[] { "绘画", "旅行", "绘画" },
            PersonalityTags = new[] { "细腻", "温和" }
        };

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{target.Id}",
            request);
        AiAccountResponse? updated = await updateResponse.Content
            .ReadFromJsonAsync<AiAccountResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(target.Id, updated.Id);
        Assert.Equal(target.CreatedAt, updated.CreatedAt);
        Assert.Equal("更新后的好友", updated.Nickname);
        Assert.Equal("Updated#01", updated.VcNumber);
        Assert.Equal("正在完善个人作品集的插画师", updated.IdentityDescription);
        Assert.Equal("Female", updated.Gender);
        Assert.Equal("Away", updated.OnlineStatus);
        Assert.Equal(2, updated.InterestTags.Count);
        Assert.Contains("旅行", updated.InterestTags);
        Assert.Contains("绘画", updated.InterestTags);

        AiAccountResponse? reloaded = await client.GetFromJsonAsync<AiAccountResponse>(
            $"/api/ai-accounts/{target.Id}");
        Assert.NotNull(reloaded);
        Assert.Equal(updated.Nickname, reloaded.Nickname);
        Assert.Equal(updated.InterestTags, reloaded.InterestTags);

        request.Nickname = existing.Nickname.ToUpperInvariant();
        using HttpResponseMessage duplicateResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{target.Id}",
            request);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        using HttpResponseMessage missingResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{Guid.NewGuid()}",
            new UpdateAiAccountRequest
            {
                Nickname = "MissingAccount",
                VcNumber = "Missing#01"
            });
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    private static async Task<AiAccountResponse> CreateAccount(
        HttpClient client,
        string nickname,
        string vcNumber)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = nickname,
                VcNumber = vcNumber
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiAccountResponse>())!;
    }
}

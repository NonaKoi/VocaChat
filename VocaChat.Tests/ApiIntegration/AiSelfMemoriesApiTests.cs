using System.Net;
using System.Net.Http.Json;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.AiSelfMemories;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证个人记忆通过真实 HTTP、Controller、Service 和 SQLite 管线工作。
/// </summary>
public sealed class AiSelfMemoriesApiTests
{
    [Fact]
    public async Task UserCanCreateEditArchiveAndReadSelfMemory()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse account = await CreateAccount(
            client,
            "SelfMemoryApi");

        CreateAiSelfMemoryRequest createRequest = new()
        {
            Type = "OngoingActivity",
            Summary = "最近正在整理旅行照片",
            FactKey = "current.travel-photos",
            FactNature = "Objective",
            Mutability = "Mutable",
            CharacterWorldId = account.CharacterWorldId,
            Salience = 75,
            IsUserLocked = true,
            OccurredAt = new DateTime(2026, 7, 20, 10, 0, 0)
        };
        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            $"/api/ai-accounts/{account.Id}/self-memories",
            createRequest);
        AiSelfMemoryResponse? created = await createResponse.Content
            .ReadFromJsonAsync<AiSelfMemoryResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        Assert.NotNull(created);
        Assert.Equal(account.Id, created.AiAccountId);
        Assert.Equal("User", created.Source);
        Assert.Equal("Active", created.Status);
        Assert.Equal("current.travel-photos", created.FactKey);
        Assert.Equal("Objective", created.FactNature);
        Assert.Equal("Mutable", created.Mutability);
        Assert.Equal("UserCanon", created.TrustLevel);
        Assert.Equal(account.CharacterWorldId, created.CharacterWorldId);
        Assert.Null(created.SourceMessageId);

        IReadOnlyList<AiSelfMemoryResponse>? createdCollection =
            await client.GetFromJsonAsync<IReadOnlyList<AiSelfMemoryResponse>>(
                createResponse.Headers.Location);
        Assert.Equal(created.Id, Assert.Single(createdCollection!).Id);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{account.Id}/self-memories/{created.Id}",
            new UpdateAiSelfMemoryRequest
            {
                Type = "Experience",
                Summary = "整理完了第一批旅行照片",
                FactKey = "current.travel-photos",
                FactNature = "Narrative",
                Mutability = "Immutable",
                CharacterWorldId = account.CharacterWorldId,
                Salience = 90,
                IsUserLocked = true,
                OccurredAt = new DateTime(2026, 7, 20, 11, 0, 0)
            });
        AiSelfMemoryResponse? updated = await updateResponse.Content
            .ReadFromJsonAsync<AiSelfMemoryResponse>();
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Experience", updated!.Type);
        Assert.Equal(90, updated.Salience);
        Assert.Equal(created.Id, updated.SupersedesMemoryId);
        Assert.NotEqual(created.Id, updated.Id);

        using HttpResponseMessage archiveResponse = await client.PutAsJsonAsync(
            $"/api/ai-accounts/{account.Id}/self-memories/{updated.Id}/status",
            new UpdateAiSelfMemoryStatusRequest { Status = "Archived" });
        AiSelfMemoryResponse? archived = await archiveResponse.Content
            .ReadFromJsonAsync<AiSelfMemoryResponse>();
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        Assert.Equal("Archived", archived!.Status);

        IReadOnlyList<AiSelfMemoryResponse>? activeMemories =
            await client.GetFromJsonAsync<IReadOnlyList<AiSelfMemoryResponse>>(
                $"/api/ai-accounts/{account.Id}/self-memories?status=Active");
        IReadOnlyList<AiSelfMemoryResponse>? archivedMemories =
            await client.GetFromJsonAsync<IReadOnlyList<AiSelfMemoryResponse>>(
                $"/api/ai-accounts/{account.Id}/self-memories?status=Archived");
        Assert.Empty(activeMemories!);
        Assert.Equal(updated.Id, Assert.Single(archivedMemories!).Id);
        IReadOnlyList<AiSelfMemoryResponse>? supersededMemories =
            await client.GetFromJsonAsync<IReadOnlyList<AiSelfMemoryResponse>>(
                $"/api/ai-accounts/{account.Id}/self-memories?status=Superseded");
        Assert.Equal(created.Id, Assert.Single(supersededMemories!).Id);
    }

    [Fact]
    public async Task InvalidDuplicateAndCrossAccountRequests_ReturnClearStatuses()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse owner = await CreateAccount(client, "MemoryOwner");
        AiAccountResponse other = await CreateAccount(client, "MemoryOther");
        CreateAiSelfMemoryRequest request = new()
        {
            Type = "PersonalFact",
            Summary = "英文名是 Luna",
            Salience = 80
        };

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            $"/api/ai-accounts/{owner.Id}/self-memories",
            request);
        AiSelfMemoryResponse created =
            (await createResponse.Content
                .ReadFromJsonAsync<AiSelfMemoryResponse>())!;
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using HttpResponseMessage duplicateResponse =
            await client.PostAsJsonAsync(
                $"/api/ai-accounts/{owner.Id}/self-memories",
                new CreateAiSelfMemoryRequest
                {
                    Type = "PersonalFact",
                    Summary = "英文名是 Luna",
                    Salience = 95
                });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using HttpResponseMessage invalidTypeResponse =
            await client.PostAsJsonAsync(
                $"/api/ai-accounts/{owner.Id}/self-memories",
                new CreateAiSelfMemoryRequest
                {
                    Type = "Unknown",
                    Summary = "不会保存",
                    Salience = 50
                });
        Assert.Equal(HttpStatusCode.BadRequest, invalidTypeResponse.StatusCode);

        using HttpResponseMessage invalidClassificationResponse =
            await client.PostAsJsonAsync(
                $"/api/ai-accounts/{owner.Id}/self-memories",
                new CreateAiSelfMemoryRequest
                {
                    Type = "Preference",
                    Summary = "不会保存的分类",
                    FactNature = "Unknown",
                    Salience = 50
                });
        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidClassificationResponse.StatusCode);

        using HttpResponseMessage missingWorldResponse =
            await client.PostAsJsonAsync(
                $"/api/ai-accounts/{owner.Id}/self-memories",
                new CreateAiSelfMemoryRequest
                {
                    Type = "Preference",
                    Summary = "不会保存到不存在的世界",
                    CharacterWorldId = Guid.NewGuid(),
                    Salience = 50
                });
        Assert.Equal(HttpStatusCode.BadRequest, missingWorldResponse.StatusCode);

        using HttpResponseMessage crossAccountResponse =
            await client.PutAsJsonAsync(
                $"/api/ai-accounts/{other.Id}/self-memories/{created.Id}",
                new UpdateAiSelfMemoryRequest
                {
                    Type = "PersonalFact",
                    Summary = "不应由其他账号修改",
                    Salience = 50
                });
        Assert.Equal(HttpStatusCode.NotFound, crossAccountResponse.StatusCode);

        using HttpResponseMessage missingAccountResponse =
            await client.GetAsync(
                $"/api/ai-accounts/{Guid.NewGuid()}/self-memories");
        Assert.Equal(HttpStatusCode.NotFound, missingAccountResponse.StatusCode);
    }

    private static async Task<AiAccountResponse> CreateAccount(
        HttpClient client,
        string prefix)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}"
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiAccountResponse>())!;
    }
}

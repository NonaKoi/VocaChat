using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.AiAccounts;
using VocaChat.WebApi.Dtos.AiWorldKnowledge;
using VocaChat.WebApi.Dtos.CharacterWorlds;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证世界认知和知识管理通过真实 HTTP、Service 与 SQLite 管线工作。
/// </summary>
public sealed class AiWorldKnowledgeApiTests
{
    [Fact]
    public async Task UserCanInspectSourcesAndGovernWorldKnowledge()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        CharacterWorldResponse subjectWorld = await CreateWorld(client);
        AiAccountResponse owner = await CreateAccount(
            client,
            "知识管理持有者",
            CharacterWorld.DefaultWorldId);
        AiAccountResponse subject = await CreateAccount(
            client,
            "知识管理讲述者",
            subjectWorld.Id);
        AiWorldKnowledge knowledge = SeedKnowledge(
            factory,
            owner.Id,
            subject.Id,
            subjectWorld.Id);

        AiWorldAwarenessOverviewResponse? overview =
            await client.GetFromJsonAsync<AiWorldAwarenessOverviewResponse>(
                $"/api/ai-accounts/{owner.Id}/world-awareness");
        Assert.NotNull(overview);
        Assert.Equal("Unaware", overview.ParallelWorld.State);
        WorldAwarenessSubjectResponse subjectOverview =
            Assert.Single(overview.Subjects);
        Assert.Equal(subject.Id, subjectOverview.AiAccountId);
        Assert.Equal("FirstImpression", subjectOverview.FamiliarityLevel);
        Assert.Equal(1, subjectOverview.ActiveKnowledgeCount);

        using HttpResponseMessage updateParallelResponse =
            await client.PutAsJsonAsync(
                $"/api/ai-accounts/{owner.Id}/world-awareness/parallel",
                new UpdateAiWorldAwarenessRequest
                {
                    State = "Accepted",
                    IsUserLocked = true
                });
        ParallelWorldAwarenessResponse? parallel =
            await updateParallelResponse.Content
                .ReadFromJsonAsync<ParallelWorldAwarenessResponse>();
        Assert.Equal(HttpStatusCode.OK, updateParallelResponse.StatusCode);
        Assert.Equal("Accepted", parallel!.State);
        Assert.True(parallel.IsUserLocked);

        using HttpResponseMessage updateSubjectResponse =
            await client.PutAsJsonAsync(
                $"/api/ai-accounts/{owner.Id}/world-awareness/subjects/{subject.Id}",
                new UpdateAiWorldAwarenessRequest
                {
                    State = "CrossWorldConfirmed",
                    IsUserLocked = true
                });
        WorldAwarenessSubjectResponse? updatedSubject =
            await updateSubjectResponse.Content
                .ReadFromJsonAsync<WorldAwarenessSubjectResponse>();
        Assert.Equal(HttpStatusCode.OK, updateSubjectResponse.StatusCode);
        Assert.Equal("CrossWorldConfirmed", updatedSubject!.AwarenessState);
        Assert.True(updatedSubject.IsUserLocked);

        IReadOnlyList<AiWorldKnowledgeResponse>? knowledgeList =
            await client.GetFromJsonAsync<
                IReadOnlyList<AiWorldKnowledgeResponse>>(
                $"/api/ai-accounts/{owner.Id}/world-knowledge"
                + $"?subjectAiAccountId={subject.Id}&status=Active");
        AiWorldKnowledgeResponse listed = Assert.Single(knowledgeList!);
        Assert.Equal(knowledge.Id, listed.Id);
        Assert.Equal(1, listed.EvidenceCount);

        IReadOnlyList<AiWorldKnowledgeEvidenceResponse>? evidence =
            await client.GetFromJsonAsync<
                IReadOnlyList<AiWorldKnowledgeEvidenceResponse>>(
                $"/api/ai-accounts/{owner.Id}/world-knowledge"
                + $"/{knowledge.Id}/evidence");
        AiWorldKnowledgeEvidenceResponse source = Assert.Single(evidence!);
        Assert.Equal("PrivateChat", source.ConversationKind);
        Assert.Equal(subject.Nickname, source.SourceDisplayName);
        Assert.Contains("沙漠", source.MessageContent);

        using HttpResponseMessage updateKnowledgeResponse =
            await client.PutAsJsonAsync(
                $"/api/ai-accounts/{owner.Id}/world-knowledge/{knowledge.Id}",
                new UpdateAiWorldKnowledgeRequest
                {
                    Summary = "用户确认：讲述者所在世界的学校受到沙漠化影响。",
                    FactNature = "ObjectiveStatement",
                    Mutability = "Constant",
                    Salience = 90,
                    IsUserLocked = true,
                    IsConfirmed = true
                });
        AiWorldKnowledgeResponse? updatedKnowledge =
            await updateKnowledgeResponse.Content
                .ReadFromJsonAsync<AiWorldKnowledgeResponse>();
        Assert.Equal(HttpStatusCode.OK, updateKnowledgeResponse.StatusCode);
        Assert.Equal("UserConfirmed", updatedKnowledge!.TrustLevel);
        Assert.True(updatedKnowledge.IsUserLocked);
        Assert.Equal(1, updatedKnowledge.EvidenceCount);

        using HttpResponseMessage unlockResponse =
            await client.PutAsJsonAsync(
                $"/api/ai-accounts/{owner.Id}/world-knowledge"
                + $"/{knowledge.Id}/lock",
                new UpdateAiWorldKnowledgeLockRequest
                {
                    IsUserLocked = false
                });
        Assert.Equal(HttpStatusCode.OK, unlockResponse.StatusCode);

        using HttpResponseMessage archiveResponse =
            await client.PutAsync(
                $"/api/ai-accounts/{owner.Id}/world-knowledge"
                + $"/{knowledge.Id}/archive",
                content: null);
        AiWorldKnowledgeResponse? archived =
            await archiveResponse.Content
                .ReadFromJsonAsync<AiWorldKnowledgeResponse>();
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        Assert.Equal("Archived", archived!.Status);
        Assert.Equal(1, archived.EvidenceCount);

        using HttpResponseMessage crossAccountResponse =
            await client.GetAsync(
                $"/api/ai-accounts/{subject.Id}/world-knowledge"
                + $"/{knowledge.Id}/evidence");
        Assert.Equal(HttpStatusCode.NotFound, crossAccountResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidAwarenessAndKnowledgeQueries_ReturnBadRequest()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();
        AiAccountResponse account = await CreateAccount(
            client,
            "无效管理请求账号",
            CharacterWorld.DefaultWorldId);

        using HttpResponseMessage invalidAwarenessResponse =
            await client.PutAsJsonAsync(
                $"/api/ai-accounts/{account.Id}/world-awareness/parallel",
                new UpdateAiWorldAwarenessRequest
                {
                    State = "Unknown"
                });
        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidAwarenessResponse.StatusCode);

        using HttpResponseMessage invalidStatusResponse =
            await client.GetAsync(
                $"/api/ai-accounts/{account.Id}/world-knowledge"
                + "?status=Unknown");
        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidStatusResponse.StatusCode);

        using HttpResponseMessage missingAccountResponse =
            await client.GetAsync(
                $"/api/ai-accounts/{Guid.NewGuid()}/world-awareness");
        Assert.Equal(
            HttpStatusCode.NotFound,
            missingAccountResponse.StatusCode);
    }

    private static AiWorldKnowledge SeedKnowledge(
        VocaChatWebApiFactory factory,
        Guid ownerId,
        Guid subjectId,
        Guid subjectWorldId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AiAccountService accountService =
            scope.ServiceProvider.GetRequiredService<AiAccountService>();
        AiAccount owner = accountService.FindById(ownerId)!;
        AiAccount subject = accountService.FindById(subjectId)!;
        PrivateChatService privateChatService =
            scope.ServiceProvider.GetRequiredService<PrivateChatService>();
        Assert.True(
            privateChatService.TryGetOrCreateAiPrivateChat(
                owner.Id,
                subject.Id,
                out PrivateChat? privateChat,
                out _,
                out string chatError),
            chatError);
        Assert.True(
            privateChatService.TrySaveAiReply(
                privateChat!,
                subject,
                "我们世界的学校一直受到沙漠化影响。",
                out PrivateMessage? message,
                out string messageError),
            messageError);

        AiWorldKnowledgeService knowledgeService =
            scope.ServiceProvider.GetRequiredService<
                AiWorldKnowledgeService>();
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            knowledgeService.TryCreateKnowledge(
                new AiWorldKnowledgeWriteData(
                    owner.Id,
                    subjectWorldId,
                    subject.Id,
                    "school.desertification",
                    "讲述者提到其世界的学校受到沙漠化影响。",
                    AiWorldKnowledgeFactNature.ObjectiveStatement,
                    AiWorldKnowledgeMutability.Constant,
                    AiWorldKnowledgeTrustLevel.DirectStatement,
                    Salience: 80,
                    IsUserLocked: false),
                message!.Id,
                sourceGroupMessageId: null,
                "私信中由讲述者直接说明学校的沙漠化问题。",
                out AiWorldKnowledge? knowledge,
                out string knowledgeError));
        Assert.Equal(string.Empty, knowledgeError);
        return Assert.IsType<AiWorldKnowledge>(knowledge);
    }

    private static async Task<CharacterWorldResponse> CreateWorld(
        HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/character-worlds",
            new CreateCharacterWorldRequest
            {
                Name = $"知识管理世界-{Guid.NewGuid():N}",
                Description = "用于世界知识 API 验收的角色世界。"
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<CharacterWorldResponse>())!;
    }

    private static async Task<AiAccountResponse> CreateAccount(
        HttpClient client,
        string prefix,
        Guid characterWorldId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/ai-accounts",
            new CreateAiAccountRequest
            {
                Nickname = $"{prefix}-{Guid.NewGuid():N}",
                CharacterWorldId = characterWorldId
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<AiAccountResponse>())!;
    }
}

using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 验证真实 HTTP 管线的模型绑定、404、OpenAPI 和测试数据库清理。
/// </summary>
public sealed class ApiInfrastructureTests
{
    [Fact]
    public async Task GroupMessageRoute_HandlesInvalidAndMissingGroupIds()
    {
        using VocaChatWebApiFactory factory = new();
        using HttpClient client = factory.CreateApiClient();

        using HttpResponseMessage invalidGuidResponse = await client.GetAsync(
            "/api/group-chats/not-a-guid/messages");
        using HttpResponseMessage missingGroupResponse = await client.GetAsync(
            $"/api/group-chats/{Guid.NewGuid()}/messages");

        Assert.Equal(HttpStatusCode.BadRequest, invalidGuidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingGroupResponse.StatusCode);
    }

    [Fact]
    public async Task OpenApi_IsAvailableAndFactoryDisposalRemovesTemporaryDatabase()
    {
        string databasePath;

        using (VocaChatWebApiFactory factory = new())
        {
            databasePath = factory.DatabasePath;
            string expectedTestDirectory = Path.Combine(
                Path.GetTempPath(),
                "VocaChat.Tests");

            Assert.StartsWith(
                expectedTestDirectory,
                databasePath,
                StringComparison.OrdinalIgnoreCase);

            using HttpClient client = factory.CreateApiClient();
            using HttpResponseMessage response = await client.GetAsync(
                "/openapi/v1.json");
            string openApiJson = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(File.Exists(databasePath));

            using JsonDocument document = JsonDocument.Parse(openApiJson);
            JsonElement paths = document.RootElement.GetProperty("paths");
            Assert.True(paths.TryGetProperty("/api/ai-accounts", out _));
            Assert.True(paths.TryGetProperty(
                "/api/ai-accounts/{id}",
                out JsonElement aiAccountByIdPath));
            Assert.True(aiAccountByIdPath.TryGetProperty("put", out _));
            Assert.True(paths.TryGetProperty("/api/group-chats", out _));
            Assert.True(paths.TryGetProperty(
                "/api/group-chats/{groupChatId}/messages",
                out _));
        }

        Assert.False(File.Exists(databasePath));
        Assert.False(File.Exists($"{databasePath}-wal"));
        Assert.False(File.Exists($"{databasePath}-shm"));
    }
}

using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

public sealed class AiInteractionDiagnosticLogServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void RecordedGenerationFailure_CanBeReadByNewServiceInstance()
    {
        Guid accountId = Guid.NewGuid();
        Guid conversationId = Guid.NewGuid();
        AiInteractionDiagnosticLogService first = new(
            _database.CreateDbContextFactory());

        Assert.True(first.TryRecord(
            AiInteractionDiagnosticSeverity.Error,
            AiInteractionDiagnosticCode.MessageGenerationFailed,
            AiMessageGenerationScenario.UserPrivateChat,
            accountId,
            conversationId,
            "回复生成失败。",
            "输出没有满足疑问句策略。"));

        AiInteractionDiagnosticLog log = Assert.Single(
            new AiInteractionDiagnosticLogService(
                _database.CreateDbContextFactory()).GetRecent());
        Assert.Equal(accountId, log.AiAccountId);
        Assert.Equal(conversationId, log.ConversationId);
        Assert.Equal("UserPrivateChat", log.Scenario);
        Assert.Contains("疑问句策略", log.Detail);
    }

    public void Dispose() => _database.Dispose();
}


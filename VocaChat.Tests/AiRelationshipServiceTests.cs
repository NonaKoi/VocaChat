using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证有方向好友关系的默认值、持久化、双向差异和互动记录。
/// </summary>
public sealed class AiRelationshipServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void MissingRelationship_ReturnsDefaultsWithoutPersistingRow()
    {
        AiAccount first = CreateAccount("RelationshipFirst");
        AiAccount second = CreateAccount("RelationshipSecond");
        AiRelationshipService service = CreateService();

        AiRelationshipOperationStatus status = service.TryGetRelationship(
            first.Id,
            second.Id,
            out AiRelationship? relationship);

        Assert.Equal(AiRelationshipOperationStatus.Success, status);
        Assert.NotNull(relationship);
        Assert.Equal(10, relationship.Familiarity);
        Assert.Equal(0, relationship.Affinity);
        Assert.Equal(10, relationship.Trust);
        Assert.Null(relationship.UpdatedAt);

        using var dbContext = _database.CreateDbContextFactory().CreateDbContext();
        Assert.Empty(dbContext.AiRelationships);
    }

    [Fact]
    public void UpdateRelationship_PersistsOnlyTheSelectedDirection()
    {
        AiAccount first = CreateAccount("DirectionalFirst");
        AiAccount second = CreateAccount("DirectionalSecond");
        AiRelationshipService service = CreateService();

        AiRelationshipOperationStatus updateStatus =
            service.TryUpdateRelationship(
                first.Id,
                second.Id,
                familiarity: 70,
                affinity: 35,
                trust: 60,
                out AiRelationship? saved);

        Assert.Equal(AiRelationshipOperationStatus.Success, updateStatus);
        Assert.NotNull(saved);

        AiRelationshipService restartedService = CreateService();
        restartedService.TryGetRelationship(
            first.Id,
            second.Id,
            out AiRelationship? reloaded);
        restartedService.TryGetRelationship(
            second.Id,
            first.Id,
            out AiRelationship? reverse);

        Assert.Equal(70, reloaded!.Familiarity);
        Assert.Equal(35, reloaded.Affinity);
        Assert.Equal(10, reverse!.Familiarity);
        Assert.Equal(0, reverse.Affinity);
    }

    [Fact]
    public void RecordInteraction_UpdatesBothDirections()
    {
        AiAccount first = CreateAccount("InteractionFirst");
        AiAccount second = CreateAccount("InteractionSecond");
        DateTime occurredAt = new(2026, 7, 17, 16, 30, 0);
        AiRelationshipService service = CreateService();

        AiRelationshipOperationStatus status = service.TryRecordInteraction(
            first.Id,
            second.Id,
            occurredAt);

        Assert.Equal(AiRelationshipOperationStatus.Success, status);
        service.TryGetRelationship(first.Id, second.Id, out AiRelationship? firstDirection);
        service.TryGetRelationship(second.Id, first.Id, out AiRelationship? secondDirection);
        Assert.Equal(1, firstDirection!.InteractionCount);
        Assert.Equal(occurredAt, firstDirection.LastInteractionAt);
        Assert.Equal(1, secondDirection!.InteractionCount);
        Assert.Equal(occurredAt, secondDirection.LastInteractionAt);
    }

    [Fact]
    public void InvalidPairsAndValuesAreRejected()
    {
        AiAccount account = CreateAccount("InvalidRelationship");
        AiRelationshipService service = CreateService();

        Assert.Equal(
            AiRelationshipOperationStatus.SelfRelationshipNotAllowed,
            service.TryGetRelationship(account.Id, account.Id, out _));
        Assert.Equal(
            AiRelationshipOperationStatus.AccountNotFound,
            service.TryGetRelationship(account.Id, Guid.NewGuid(), out _));
        Assert.Equal(
            AiRelationshipOperationStatus.ValueOutOfRange,
            service.TryUpdateRelationship(
                account.Id,
                Guid.NewGuid(),
                familiarity: 101,
                affinity: 0,
                trust: 10,
                out _));
    }

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        bool succeeded = service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private AiRelationshipService CreateService()
    {
        return new AiRelationshipService(_database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

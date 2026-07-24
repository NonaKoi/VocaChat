using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证分级记忆 Migration 能安全接管已有个人记忆，而不是只验证全新数据库。
/// </summary>
public sealed class AiSelfMemoryMigrationTests
{
    [Fact]
    public void AddScopedSelfMemoryFacts_BackfillsExistingMemoryWithoutDataLoss()
    {
        using SqliteTestDatabase database = new(applyMigrations: false);
        VocaChatDbContextFactory factory = database.CreateDbContextFactory();
        using (VocaChatDbContext dbContext = factory.CreateDbContext())
        {
            dbContext.GetService<IMigrator>().Migrate(
                "20260723095131_AddCharacterWorlds");
        }

        AiAccountService accountService = new(factory);
        Assert.True(
            accountService.TryCreateAiAccount(
                $"MigrationMemory-{Guid.NewGuid():N}",
                string.Empty,
                string.Empty,
                string.Empty,
                out AiAccount? account,
                out string accountError),
            accountError);

        Guid memoryId = Guid.NewGuid();
        DateTime createdAt = new(2026, 7, 20, 9, 0, 0);
        SqliteConnectionStringBuilder connectionString = new()
        {
            DataSource = database.DatabasePath,
            ForeignKeys = true,
            Pooling = false
        };
        using (SqliteConnection connection = new(
                   connectionString.ToString()))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO "AiSelfMemories" (
                    "Id", "AiAccountId", "Type", "Summary", "Source",
                    "Status", "Salience", "IsUserLocked",
                    "SourceConversationId", "SourceMessageId",
                    "OccurredAt", "ValidFrom", "ValidUntil",
                    "CreatedAt", "UpdatedAt")
                VALUES (
                    $id, $accountId, $type, $summary, $source,
                    $status, $salience, $isUserLocked,
                    NULL, NULL, NULL, NULL, NULL, $createdAt, $updatedAt);
                """;
            command.Parameters.AddWithValue("$id", memoryId);
            command.Parameters.AddWithValue("$accountId", account!.Id);
            command.Parameters.AddWithValue(
                "$type",
                (int)AiSelfMemoryType.Preference);
            command.Parameters.AddWithValue("$summary", "喜欢雨天散步");
            command.Parameters.AddWithValue(
                "$source",
                (int)AiSelfMemorySource.Director);
            command.Parameters.AddWithValue(
                "$status",
                (int)AiSelfMemoryStatus.Active);
            command.Parameters.AddWithValue("$salience", 70);
            command.Parameters.AddWithValue("$isUserLocked", false);
            command.Parameters.AddWithValue("$createdAt", createdAt);
            command.Parameters.AddWithValue("$updatedAt", createdAt);
            Assert.Equal(1, command.ExecuteNonQuery());
        }

        using (VocaChatDbContext dbContext = factory.CreateDbContext())
        {
            dbContext.GetService<IMigrator>().Migrate();
        }

        AiSelfMemoryService memoryService = new(factory);
        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            memoryService.TryGetMemories(
                account!.Id,
                10,
                status: null,
                out IReadOnlyList<AiSelfMemory> memories,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);

        AiSelfMemory migrated = Assert.Single(memories);
        Assert.Equal(memoryId, migrated.Id);
        Assert.Equal("喜欢雨天散步", migrated.Summary);
        Assert.Equal(CharacterWorld.DefaultWorldId, migrated.CharacterWorldId);
        Assert.Equal(
            $"legacy.{memoryId:N}",
            migrated.FactKey);
        Assert.Equal(AiSelfMemoryFactNature.Subjective, migrated.FactNature);
        Assert.Equal(AiSelfMemoryMutability.Evolving, migrated.Mutability);
        Assert.Equal(
            AiSelfMemoryTrustLevel.SubjectiveState,
            migrated.TrustLevel);
        Assert.Null(migrated.SupersedesMemoryId);
    }
}

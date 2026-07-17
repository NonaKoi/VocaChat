using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证 DbContext 基础创建方式，不接触正式开发数据库文件。
/// </summary>
public class VocaChatDbContextTests
{
    [Fact]
    public void CreateDbContext_WithTestConnectionString_UsesSqliteProvider()
    {
        VocaChatDbContextFactory factory = new("Data Source=:memory:");

        using VocaChatDbContext context = factory.CreateDbContext();

        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
        Assert.Equal(":memory:", context.Database.GetDbConnection().DataSource);
        Assert.True(context.Database.CanConnect());
    }

    [Fact]
    public void AddAiAccountVcNumberMigration_BackfillsExistingAccounts()
    {
        using SqliteTestDatabase database = new(applyMigrations: false);
        VocaChatDbContextFactory factory = database.CreateDbContextFactory();

        using (VocaChatDbContext oldContext = factory.CreateDbContext())
        {
            IMigrator migrator = oldContext.GetService<IMigrator>();
            migrator.Migrate("20260714062031_AddGroupMessages");

            Guid accountId = Guid.NewGuid();
            oldContext.Database.ExecuteSqlInterpolated(
                $"""
                INSERT INTO "AiAccounts"
                    ("Id", "Nickname", "IdentityDescription", "Personality", "SpeakingStyle", "CreatedAt")
                VALUES
                    ({accountId}, {"ExistingFriend"}, {string.Empty}, {string.Empty}, {string.Empty}, {DateTime.Now});
                """);

            migrator.Migrate();
        }

        using VocaChatDbContext upgradedContext = factory.CreateDbContext();
        AiAccount account = Assert.Single(upgradedContext.AiAccounts.AsNoTracking());

        Assert.Equal("ExistingFriend", account.Nickname);
        Assert.Matches("^[0-9]{7}$", account.VcNumber);
        Assert.Equal(string.Empty, account.Signature);
        Assert.Null(account.Birthday);
        Assert.Equal(AiAccountGender.Unspecified, account.Gender);
        Assert.Equal(string.Empty, account.Location);
        Assert.Equal(string.Empty, account.Occupation);
        Assert.Equal(string.Empty, account.Hometown);
        Assert.Equal(OnlineStatus.Offline, account.OnlineStatus);
        Assert.Empty(account.Tags);

        Contact backfilledContact = Assert.Single(
            upgradedContext.Contacts.AsNoTracking());
        Assert.NotNull(new ContactService(factory).FindById(backfilledContact.Id));
    }
}

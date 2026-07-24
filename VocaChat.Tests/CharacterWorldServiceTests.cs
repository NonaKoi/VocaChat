using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证角色世界持久化、共享引用和账号关联规则。
/// </summary>
public sealed class CharacterWorldServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void DefaultWorld_IsSeededAndPersistsAcrossServiceInstances()
    {
        CharacterWorldService firstService = CreateWorldService();

        CharacterWorld? firstRead = firstService.FindById(
            CharacterWorld.DefaultWorldId);
        CharacterWorld? secondRead = CreateWorldService().FindById(
            CharacterWorld.DefaultWorldId);

        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        Assert.Equal(CharacterWorld.DefaultWorldName, firstRead.Name);
        Assert.Equal(
            CharacterWorld.DefaultWorldDescription,
            secondRead.Description);
        Assert.NotSame(firstRead, secondRead);
    }

    [Fact]
    public void Migration_AssignsExistingAccountsToDefaultWorld()
    {
        using SqliteTestDatabase database = new(applyMigrations: false);
        VocaChatDbContextFactory factory = database.CreateDbContextFactory();
        Guid accountId = Guid.NewGuid();

        using (VocaChatDbContext dbContext = factory.CreateDbContext())
        {
            IMigrator migrator = dbContext.GetService<IMigrator>();
            migrator.Migrate("20260723051948_AddAiModelTokenUsage");
            dbContext.Database.ExecuteSqlInterpolated($"""
                INSERT INTO "AiAccounts"
                (
                    "Id",
                    "VcNumber",
                    "Nickname",
                    "IdentityDescription",
                    "Personality",
                    "SpeakingStyle",
                    "Signature",
                    "Gender",
                    "Location",
                    "Occupation",
                    "Hometown",
                    "OnlineStatus",
                    "CreatedAt"
                )
                VALUES
                (
                    {accountId},
                    {"Legacy#01"},
                    {"迁移前账号"},
                    {string.Empty},
                    {string.Empty},
                    {string.Empty},
                    {string.Empty},
                    {(int)AiAccountGender.Unspecified},
                    {string.Empty},
                    {string.Empty},
                    {string.Empty},
                    {(int)OnlineStatus.Offline},
                    {new DateTime(2026, 7, 22, 12, 0, 0)}
                );
                """);

            migrator.Migrate();
        }

        AiAccount? migratedAccount = new AiAccountService(factory)
            .FindById(accountId);

        Assert.NotNull(migratedAccount);
        Assert.Equal(
            CharacterWorld.DefaultWorldId,
            migratedAccount.CharacterWorldId);
        Assert.Equal(
            CharacterWorld.DefaultWorldName,
            migratedAccount.CharacterWorld?.Name);
    }

    [Fact]
    public void TryCreateAndUpdate_PersistsWorldAndRejectsDuplicateName()
    {
        CharacterWorldService service = CreateWorldService();

        CharacterWorldOperationStatus createStatus = service.TryCreate(
            "  自创世界  ",
            "  漂浮城市由潮汐能源驱动。  ",
            out CharacterWorld? created,
            out string createError);

        Assert.Equal(CharacterWorldOperationStatus.Success, createStatus);
        Assert.NotNull(created);
        Assert.Equal(string.Empty, createError);
        Assert.Equal("自创世界", created.Name);

        CharacterWorldOperationStatus duplicateStatus = service.TryCreate(
            "自创世界",
            string.Empty,
            out CharacterWorld? duplicate,
            out string duplicateError);
        Assert.Equal(
            CharacterWorldOperationStatus.DuplicateName,
            duplicateStatus);
        Assert.Null(duplicate);
        Assert.Equal("角色世界名称已存在。", duplicateError);

        CharacterWorldOperationStatus updateStatus = service.TryUpdate(
            created.Id,
            "群岛纪元",
            "浮空群岛依靠潮汐核心维持航行。",
            out CharacterWorld? updated,
            out string updateError);

        Assert.Equal(CharacterWorldOperationStatus.Success, updateStatus);
        Assert.NotNull(updated);
        Assert.Equal(string.Empty, updateError);

        CharacterWorld? stored = CreateWorldService().FindById(created.Id);
        Assert.NotNull(stored);
        Assert.Equal("群岛纪元", stored.Name);
        Assert.Equal("浮空群岛依靠潮汐核心维持航行。", stored.Description);
        Assert.True(stored.UpdatedAt >= stored.CreatedAt);
    }

    [Fact]
    public void Accounts_CanShareExistingWorldWithoutCopyingIt()
    {
        CharacterWorldService worldService = CreateWorldService();
        Assert.Equal(
            CharacterWorldOperationStatus.Success,
            worldService.TryCreate(
                "基沃托斯",
                "用户定义的学园都市设定。",
                out CharacterWorld? world,
                out _));
        Assert.NotNull(world);

        AiAccountService accountService = CreateAccountService();
        AiAccount first = CreateAccount(
            accountService,
            "第一位好友",
            world.Id);
        AiAccount second = CreateAccount(
            accountService,
            "第二位好友",
            world.Id);

        AiAccount? storedFirst = CreateAccountService().FindById(first.Id);
        AiAccount? storedSecond = CreateAccountService().FindById(second.Id);
        IReadOnlyList<CharacterWorld> worlds =
            CreateWorldService().GetAll();

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.Equal(world.Id, storedFirst.CharacterWorldId);
        Assert.Equal(world.Id, storedSecond.CharacterWorldId);
        Assert.Equal(world.Id, storedFirst.CharacterWorld?.Id);
        Assert.Equal(world.Id, storedSecond.CharacterWorld?.Id);
        Assert.Equal(2, worlds.Count);
    }

    [Fact]
    public void AccountCreation_WithMissingWorld_IsRejected()
    {
        AiAccountService accountService = CreateAccountService();

        bool succeeded = accountService.TryCreateAiAccount(
            new AiAccountCreationData
            {
                Nickname = "无效世界好友",
                CharacterWorldId = Guid.NewGuid()
            },
            out AiAccount? account,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(account);
        Assert.Equal("角色世界不存在。", errorMessage);
        Assert.Empty(accountService.GetAllAccounts());
    }

    [Fact]
    public void AccountUpdate_CanChangeWorldAndPreservesItWhenOmitted()
    {
        CharacterWorldService worldService = CreateWorldService();
        Assert.Equal(
            CharacterWorldOperationStatus.Success,
            worldService.TryCreate(
                "远海世界",
                "大陆被海洋分隔。",
                out CharacterWorld? world,
                out _));
        Assert.NotNull(world);

        AiAccountService accountService = CreateAccountService();
        AiAccount account = CreateAccount(
            accountService,
            "航海者",
            CharacterWorld.DefaultWorldId);

        AiAccountUpdateStatus firstStatus =
            accountService.TryUpdateAiAccount(
                account.Id,
                CreateUpdateData(account, world.Id),
                out AiAccount? updated,
                out string firstError);
        Assert.Equal(AiAccountUpdateStatus.Success, firstStatus);
        Assert.NotNull(updated);
        Assert.Equal(string.Empty, firstError);
        Assert.Equal(world.Id, updated.CharacterWorldId);

        AiAccountUpdateStatus secondStatus =
            accountService.TryUpdateAiAccount(
                account.Id,
                CreateUpdateData(updated, characterWorldId: null),
                out AiAccount? preserved,
                out string secondError);
        Assert.Equal(AiAccountUpdateStatus.Success, secondStatus);
        Assert.NotNull(preserved);
        Assert.Equal(string.Empty, secondError);
        Assert.Equal(world.Id, preserved.CharacterWorldId);
    }

    private AiAccountService CreateAccountService() =>
        new(_database.CreateDbContextFactory());

    private CharacterWorldService CreateWorldService() =>
        new(_database.CreateDbContextFactory());

    private static AiAccount CreateAccount(
        AiAccountService service,
        string nickname,
        Guid characterWorldId)
    {
        bool succeeded = service.TryCreateAiAccount(
            new AiAccountCreationData
            {
                Nickname = nickname,
                CharacterWorldId = characterWorldId
            },
            out AiAccount? account,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private static AiAccountUpdateData CreateUpdateData(
        AiAccount account,
        Guid? characterWorldId)
    {
        return new AiAccountUpdateData
        {
            Nickname = account.Nickname,
            VcNumber = account.VcNumber,
            IdentityDescription = account.IdentityDescription,
            Personality = account.Personality,
            SpeakingStyle = account.SpeakingStyle,
            Signature = account.Signature,
            Birthday = account.Birthday,
            Gender = account.Gender,
            Location = account.Location,
            Occupation = account.Occupation,
            Hometown = account.Hometown,
            OnlineStatus = account.OnlineStatus,
            InterestTags = account.Tags
                .Where(tag => tag.Type == AiAccountTagType.Interest)
                .Select(tag => tag.Value)
                .ToArray(),
            PersonalityTags = account.Tags
                .Where(tag => tag.Type == AiAccountTagType.Personality)
                .Select(tag => tag.Value)
                .ToArray(),
            CharacterWorldId = characterWorldId
        };
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证 AI 账号的创建、昵称规则、查询和集合保护。
/// </summary>
public class AiAccountServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void TryCreateAiAccount_WithValidData_CreatesAndStoresAccount()
    {
        AiAccountService service = CreateService();

        bool succeeded = service.TryCreateAiAccount(
            "  Nona  ",
            "  助手  ",
            "  冷静  ",
            "  简洁  ",
            out AiAccount? account,
            out string errorMessage);

        Assert.True(succeeded);
        Assert.NotNull(account);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal("Nona", account.Nickname);
        Assert.Equal("助手", account.IdentityDescription);
        Assert.Equal("冷静", account.Personality);
        Assert.Equal("简洁", account.SpeakingStyle);
        Assert.Equal(account.Id, Assert.Single(service.GetAllAccounts()).Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreateAiAccount_WithBlankNickname_Fails(string nickname)
    {
        AiAccountService service = CreateService();

        bool succeeded = service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(account);
        Assert.Equal("昵称不能为空。", errorMessage);
        Assert.Empty(service.GetAllAccounts());
    }

    [Fact]
    public void TryCreateAiAccount_WithDuplicateNicknameIgnoringCase_Fails()
    {
        AiAccountService service = CreateService();
        CreateAccount(service, "Nona");

        bool succeeded = service.TryCreateAiAccount(
            "nOnA",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? duplicateAccount,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(duplicateAccount);
        Assert.Equal("昵称已存在。", errorMessage);
        Assert.Single(service.GetAllAccounts());
    }

    [Fact]
    public void FindById_WithExistingId_ReturnsAccount()
    {
        AiAccountService service = CreateService();
        AiAccount account = CreateAccount(service, "Nona");

        AiAccount? foundAccount = service.FindById(account.Id);

        Assert.NotNull(foundAccount);
        Assert.Equal(account.Id, foundAccount.Id);
        Assert.NotSame(account, foundAccount);
    }

    [Fact]
    public void FindByNickname_IgnoresCaseAndSpaces()
    {
        AiAccountService service = CreateService();
        AiAccount account = CreateAccount(service, "Nona");

        AiAccount? foundAccount = service.FindByNickname("  NONA  ");

        Assert.NotNull(foundAccount);
        Assert.Equal(account.Id, foundAccount.Id);
    }

    [Fact]
    public void FindMethods_WithMissingAccount_ReturnNull()
    {
        AiAccountService service = CreateService();

        Assert.Null(service.FindById(Guid.NewGuid()));
        Assert.Null(service.FindByNickname("不存在"));
    }

    [Fact]
    public void GetAllAccounts_ReturnsReadOnlySnapshot()
    {
        AiAccountService service = CreateService();
        CreateAccount(service, "Nona");
        IReadOnlyList<AiAccount> snapshot = service.GetAllAccounts();
        IList<AiAccount> mutableView = Assert.IsAssignableFrom<IList<AiAccount>>(snapshot);
        AiAccount externalAccount = new("External", string.Empty, string.Empty, string.Empty);

        Assert.Throws<NotSupportedException>(() => mutableView.Add(externalAccount));

        CreateAccount(service, "Mika");
        Assert.Single(snapshot);
        Assert.Equal(2, service.GetAllAccounts().Count);
    }

    [Fact]
    public void Account_CanBeReadByNewServiceUsingSameDatabase()
    {
        AiAccountService firstService = CreateService();
        bool succeeded = firstService.TryCreateAiAccount(
            "Nona",
            "助手",
            "冷静",
            "简洁",
            out AiAccount? createdAccount,
            out string errorMessage);
        Assert.True(succeeded, errorMessage);
        Assert.NotNull(createdAccount);
        AiAccountService restartedService = new(_database.CreateDbContextFactory());

        AiAccount? storedAccount = restartedService.FindById(createdAccount.Id);

        Assert.NotNull(storedAccount);
        Assert.Equal(createdAccount.Id, storedAccount.Id);
        Assert.Equal("Nona", storedAccount.Nickname);
        Assert.Equal("助手", storedAccount.IdentityDescription);
        Assert.Equal("冷静", storedAccount.Personality);
        Assert.Equal("简洁", storedAccount.SpeakingStyle);
        Assert.Equal(createdAccount.CreatedAt, storedAccount.CreatedAt);
    }

    [Fact]
    public void DatabaseUniqueIndex_RejectsDuplicateNicknameIgnoringCase()
    {
        AiAccountService service = CreateService();
        CreateAccount(service, "Nona");

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        dbContext.AiAccounts.Add(
            new AiAccount("nOnA", string.Empty, string.Empty, string.Empty));

        Assert.Throws<DbUpdateException>(() => dbContext.SaveChanges());
    }

    /// <summary>
    /// 创建连接当前测试独立数据库的账号 Service。
    /// </summary>
    private AiAccountService CreateService()
    {
        return new AiAccountService(_database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    /// <summary>
    /// 创建测试所需账号，并确保测试准备本身成功。
    /// </summary>
    private static AiAccount CreateAccount(AiAccountService service, string nickname)
    {
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
}

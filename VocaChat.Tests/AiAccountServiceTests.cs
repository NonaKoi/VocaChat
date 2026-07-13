using System;
using System.Collections.Generic;
using System.Linq;
using VocaChat.ConsoleApp.Models;
using VocaChat.ConsoleApp.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证 AI 账号的创建、昵称规则、查询和集合保护。
/// </summary>
public class AiAccountServiceTests
{
    [Fact]
    public void TryCreateAiAccount_WithValidData_CreatesAndStoresAccount()
    {
        AiAccountService service = new();

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
        Assert.Same(account, Assert.Single(service.GetAllAccounts()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreateAiAccount_WithBlankNickname_Fails(string nickname)
    {
        AiAccountService service = new();

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
        AiAccountService service = new();
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
        AiAccountService service = new();
        AiAccount account = CreateAccount(service, "Nona");

        AiAccount? foundAccount = service.FindById(account.Id);

        Assert.Same(account, foundAccount);
    }

    [Fact]
    public void FindByNickname_IgnoresCaseAndSpaces()
    {
        AiAccountService service = new();
        AiAccount account = CreateAccount(service, "Nona");

        AiAccount? foundAccount = service.FindByNickname("  NONA  ");

        Assert.Same(account, foundAccount);
    }

    [Fact]
    public void FindMethods_WithMissingAccount_ReturnNull()
    {
        AiAccountService service = new();

        Assert.Null(service.FindById(Guid.NewGuid()));
        Assert.Null(service.FindByNickname("不存在"));
    }

    [Fact]
    public void GetAllAccounts_ReturnsReadOnlySnapshot()
    {
        AiAccountService service = new();
        CreateAccount(service, "Nona");
        IReadOnlyList<AiAccount> snapshot = service.GetAllAccounts();
        IList<AiAccount> mutableView = Assert.IsAssignableFrom<IList<AiAccount>>(snapshot);
        AiAccount externalAccount = new("External", string.Empty, string.Empty, string.Empty);

        Assert.Throws<NotSupportedException>(() => mutableView.Add(externalAccount));

        CreateAccount(service, "Mika");
        Assert.Single(snapshot);
        Assert.Equal(2, service.GetAllAccounts().Count);
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

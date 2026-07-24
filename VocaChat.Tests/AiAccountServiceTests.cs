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
        Assert.Matches("^[0-9]{7}$", account.VcNumber);
        Assert.Equal("Nona", account.Nickname);
        Assert.Equal("助手", account.IdentityDescription);
        Assert.Equal("冷静", account.Personality);
        Assert.Equal("简洁", account.SpeakingStyle);
        Assert.Equal(
            CharacterWorld.DefaultWorldId,
            account.CharacterWorldId);
        Assert.Equal(
            CharacterWorld.DefaultWorldName,
            account.CharacterWorld?.Name);
        Assert.Equal(account.Id, Assert.Single(service.GetAllAccounts()).Id);
    }

    [Fact]
    public void TryCreateAiAccount_WithCustomVcNumber_AcceptsLettersAndSymbols()
    {
        AiAccountService service = CreateService();

        bool succeeded = service.TryCreateAiAccount(
            "Nona",
            "  Nona#_2026  ",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(account);
        Assert.Equal("Nona#_2026", account.VcNumber);
        Assert.Equal(account.Id, service.FindByVcNumber("nona#_2026")?.Id);
    }

    [Fact]
    public void TryCreateAiAccount_WithCompleteProfile_PersistsStructuredProfile()
    {
        AiAccountService service = CreateService();
        AiAccountCreationData creationData = new()
        {
            Nickname = "  夜影  ",
            VcNumber = "Night#01",
            IdentityDescription = "  喜欢安静思考的朋友  ",
            Personality = "  冷静、理性  ",
            SpeakingStyle = "  简洁温和  ",
            Signature = "  在自己的节奏里前进。  ",
            Birthday = new DateOnly(2000, 7, 23),
            Gender = AiAccountGender.Male,
            Location = "  中国 上海  ",
            Occupation = "  自由插画师  ",
            Hometown = "  中国 杭州  ",
            OnlineStatus = OnlineStatus.Online,
            InterestTags = new[] { " 绘画 ", "阅读", "绘画" },
            PersonalityTags = new[] { "冷静", "理性", "冷静" }
        };

        bool succeeded = service.TryCreateAiAccount(
            creationData,
            out AiAccount? createdAccount,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(createdAccount);

        AiAccount? storedAccount = new AiAccountService(
            _database.CreateDbContextFactory()).FindById(createdAccount.Id);

        Assert.NotNull(storedAccount);
        Assert.Equal("在自己的节奏里前进。", storedAccount.Signature);
        Assert.Equal(new DateOnly(2000, 7, 23), storedAccount.Birthday);
        Assert.Equal(AiAccountGender.Male, storedAccount.Gender);
        Assert.Equal("中国 上海", storedAccount.Location);
        Assert.Equal("自由插画师", storedAccount.Occupation);
        Assert.Equal("中国 杭州", storedAccount.Hometown);
        Assert.Equal(OnlineStatus.Online, storedAccount.OnlineStatus);
        Assert.Equal(2, storedAccount.Tags.Count(tag =>
            tag.Type == AiAccountTagType.Interest));
        Assert.Equal(2, storedAccount.Tags.Count(tag =>
            tag.Type == AiAccountTagType.Personality));
        Assert.Equal(25, storedAccount.CalculateAge(new DateOnly(2026, 7, 22)));
        Assert.Equal(26, storedAccount.CalculateAge(new DateOnly(2026, 7, 23)));
        Assert.Equal("狮子座", storedAccount.GetZodiacSign());
    }

    [Fact]
    public void TryCreateAiAccount_WithFutureBirthday_Fails()
    {
        AiAccountService service = CreateService();

        bool succeeded = service.TryCreateAiAccount(
            new AiAccountCreationData
            {
                Nickname = "FutureFriend",
                Birthday = DateOnly.FromDateTime(DateTime.Today).AddDays(1)
            },
            out AiAccount? account,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(account);
        Assert.Equal("生日不能晚于今天。", errorMessage);
    }

    [Fact]
    public void TryCreateAiAccount_WithoutVcNumber_GeneratesDistinctDefaults()
    {
        AiAccountService service = CreateService();

        AiAccount firstAccount = CreateAccount(service, "Nona");
        AiAccount secondAccount = CreateAccount(service, "Mika");

        Assert.Matches("^[0-9]{7}$", firstAccount.VcNumber);
        Assert.Matches("^[0-9]{7}$", secondAccount.VcNumber);
        Assert.NotEqual(firstAccount.VcNumber, secondAccount.VcNumber);
    }

    [Fact]
    public void TryCreateAiAccount_WithDuplicateVcNumberIgnoringCase_Fails()
    {
        AiAccountService service = CreateService();
        CreateAccountWithVcNumber(service, "Nona", "Friend#01");

        bool succeeded = service.TryCreateAiAccount(
            "Mika",
            "friend#01",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? duplicateAccount,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(duplicateAccount);
        Assert.Equal("VC号已存在。", errorMessage);
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
        AiAccount externalAccount = new(
            "External#01",
            "External",
            string.Empty,
            string.Empty,
            string.Empty);

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
        Assert.Equal(createdAccount.VcNumber, storedAccount.VcNumber);
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
            new AiAccount(
                "DifferentVcNumber",
                "nOnA",
                string.Empty,
                string.Empty,
                string.Empty));

        Assert.Throws<DbUpdateException>(() => dbContext.SaveChanges());
    }

    [Fact]
    public void DatabaseUniqueIndex_RejectsDuplicateVcNumberIgnoringCase()
    {
        AiAccountService service = CreateService();
        CreateAccountWithVcNumber(service, "Nona", "Friend#01");

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        dbContext.AiAccounts.Add(
            new AiAccount(
                "friend#01",
                "Mika",
                string.Empty,
                string.Empty,
                string.Empty));

        Assert.Throws<DbUpdateException>(() => dbContext.SaveChanges());
    }

    [Fact]
    public void TryChangeVcNumber_UpdatesDisplayIdWithoutChangingInternalId()
    {
        AiAccountService service = CreateService();
        AiAccount account = CreateAccount(service, "Nona");

        bool succeeded = service.TryChangeVcNumber(
            account.Id,
            "Nona@Home",
            out AiAccount? updatedAccount,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(updatedAccount);
        Assert.Equal(account.Id, updatedAccount.Id);
        Assert.Equal("Nona@Home", updatedAccount.VcNumber);
        Assert.Equal(account.Id, service.FindByVcNumber("nona@home")?.Id);
    }

    [Fact]
    public void TryUpdateAiAccount_ReplacesProfileAndTagsWithoutChangingIdentityOrMedia()
    {
        AiAccountService service = CreateService();
        Assert.True(service.TryCreateAiAccount(
            new AiAccountCreationData
            {
                Nickname = "旧昵称",
                VcNumber = "Old#Profile",
                Signature = "旧签名",
                InterestTags = new[] { "绘画", "阅读" },
                PersonalityTags = new[] { "安静" }
            },
            out AiAccount? created,
            out string createError), createError);
        Assert.NotNull(created);
        Assert.True(service.TryChangeAvatarMediaId(
            created.Id,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png",
            out _,
            out _,
            out string mediaError), mediaError);
        DateTime originalCreatedAt = created.CreatedAt;

        AiAccountUpdateStatus status = service.TryUpdateAiAccount(
            created.Id,
            new AiAccountUpdateData
            {
                Nickname = "  新昵称  ",
                VcNumber = "  New#Profile  ",
                IdentityDescription = "  长期生活在海边的插画师  ",
                Personality = "  温和、认真  ",
                SpeakingStyle = "  自然简洁  ",
                Signature = "  慢慢画完想画的故事。  ",
                Birthday = new DateOnly(1999, 11, 8),
                Gender = AiAccountGender.Female,
                Location = "  中国 厦门  ",
                Occupation = "  插画师  ",
                Hometown = "  中国 泉州  ",
                OnlineStatus = OnlineStatus.Away,
                InterestTags = new[] { "摄影", " 阅读 ", "摄影" },
                PersonalityTags = new[] { "细腻", "温和" }
            },
            out AiAccount? updated,
            out string updateError);

        Assert.Equal(AiAccountUpdateStatus.Success, status);
        Assert.Equal(string.Empty, updateError);
        Assert.NotNull(updated);
        AiAccount stored = Assert.IsType<AiAccount>(
            new AiAccountService(_database.CreateDbContextFactory())
                .FindById(created.Id));
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal(originalCreatedAt, stored.CreatedAt);
        Assert.Equal("New#Profile", stored.VcNumber);
        Assert.Equal("新昵称", stored.Nickname);
        Assert.Equal("长期生活在海边的插画师", stored.IdentityDescription);
        Assert.Equal("慢慢画完想画的故事。", stored.Signature);
        Assert.Equal(new DateOnly(1999, 11, 8), stored.Birthday);
        Assert.Equal(AiAccountGender.Female, stored.Gender);
        Assert.Equal(OnlineStatus.Away, stored.OnlineStatus);
        Assert.Equal(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png",
            stored.AvatarMediaId);
        Assert.Equal(
            new[] { "摄影", "阅读" },
            stored.Tags
                .Where(tag => tag.Type == AiAccountTagType.Interest)
                .OrderBy(tag => tag.Value)
                .Select(tag => tag.Value));
        Assert.Equal(
            new[] { "温和", "细腻" },
            stored.Tags
                .Where(tag => tag.Type == AiAccountTagType.Personality)
                .OrderBy(tag => tag.Value)
                .Select(tag => tag.Value));
        Assert.DoesNotContain(stored.Tags, tag => tag.Value == "绘画");

        Assert.Equal(
            AiAccountUpdateStatus.Success,
            service.TryUpdateAiAccount(
                stored.Id,
                CreateUpdateData(stored.Nickname, stored.VcNumber),
                out _,
                out _));
    }

    [Fact]
    public void TryUpdateAiAccount_DuplicateIdentityValuesAreRejectedWithoutPartialChange()
    {
        AiAccountService service = CreateService();
        AiAccount first = CreateAccountWithVcNumber(
            service,
            "FirstAccount",
            "First#01");
        AiAccount second = CreateAccountWithVcNumber(
            service,
            "SecondAccount",
            "Second#02");

        AiAccountUpdateStatus nicknameStatus = service.TryUpdateAiAccount(
            second.Id,
            CreateUpdateData("firstaccount", "Second#02"),
            out _,
            out string nicknameError);
        AiAccountUpdateStatus vcNumberStatus = service.TryUpdateAiAccount(
            second.Id,
            CreateUpdateData("SecondAccount", "first#01"),
            out _,
            out string vcNumberError);

        Assert.Equal(AiAccountUpdateStatus.DuplicateNickname, nicknameStatus);
        Assert.Equal("昵称已存在。", nicknameError);
        Assert.Equal(AiAccountUpdateStatus.DuplicateVcNumber, vcNumberStatus);
        Assert.Equal("VC号已存在。", vcNumberError);
        AiAccount unchanged = Assert.IsType<AiAccount>(service.FindById(second.Id));
        Assert.Equal("SecondAccount", unchanged.Nickname);
        Assert.Equal("Second#02", unchanged.VcNumber);
        Assert.NotEqual(first.Id, unchanged.Id);
    }

    [Fact]
    public void TryUpdateAiAccount_InvalidDataAndMissingAccountReturnExplicitStatuses()
    {
        AiAccountService service = CreateService();
        AiAccount account = CreateAccount(service, "UpdateValidation");

        Assert.Equal(
            AiAccountUpdateStatus.InvalidData,
            service.TryUpdateAiAccount(
                account.Id,
                CreateUpdateData("   ", account.VcNumber),
                out _,
                out _));
        Assert.Equal(
            AiAccountUpdateStatus.InvalidData,
            service.TryUpdateAiAccount(
                account.Id,
                CreateUpdateData(account.Nickname, "   "),
                out _,
                out _));
        Assert.Equal(
            AiAccountUpdateStatus.InvalidData,
            service.TryUpdateAiAccount(
                account.Id,
                new AiAccountUpdateData
                {
                    Nickname = account.Nickname,
                    VcNumber = account.VcNumber,
                    Birthday = DateOnly.FromDateTime(DateTime.Today).AddDays(1)
                },
                out _,
                out _));
        Assert.Equal(
            AiAccountUpdateStatus.AccountNotFound,
            service.TryUpdateAiAccount(
                Guid.NewGuid(),
                CreateUpdateData("MissingAccount", "Missing#01"),
                out _,
                out _));
    }

    [Fact]
    public void TryChangeMediaIds_PersistsOpaqueIdentifiersAcrossServiceInstances()
    {
        AiAccountService firstService = CreateService();
        AiAccount account = CreateAccount(firstService, "MediaNona");

        bool avatarChanged = firstService.TryChangeAvatarMediaId(
            account.Id,
            "11111111111111111111111111111111.png",
            out _,
            out string? previousAvatarMediaId,
            out string avatarError);
        bool coverChanged = firstService.TryChangeProfileCoverMediaId(
            account.Id,
            "22222222222222222222222222222222.webp",
            out _,
            out string? previousCoverMediaId,
            out string coverError);

        AiAccount? reloadedAccount = CreateService().FindById(account.Id);

        Assert.True(avatarChanged, avatarError);
        Assert.True(coverChanged, coverError);
        Assert.Null(previousAvatarMediaId);
        Assert.Null(previousCoverMediaId);
        Assert.NotNull(reloadedAccount);
        Assert.Equal(
            "11111111111111111111111111111111.png",
            reloadedAccount.AvatarMediaId);
        Assert.Equal(
            "22222222222222222222222222222222.webp",
            reloadedAccount.ProfileCoverMediaId);
        Assert.False(Path.IsPathRooted(reloadedAccount.AvatarMediaId));
        Assert.False(Path.IsPathRooted(reloadedAccount.ProfileCoverMediaId));
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

    private static AiAccount CreateAccountWithVcNumber(
        AiAccountService service,
        string nickname,
        string vcNumber)
    {
        bool succeeded = service.TryCreateAiAccount(
            nickname,
            vcNumber,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private static AiAccountUpdateData CreateUpdateData(
        string nickname,
        string vcNumber)
    {
        return new AiAccountUpdateData
        {
            Nickname = nickname,
            VcNumber = vcNumber
        };
    }
}

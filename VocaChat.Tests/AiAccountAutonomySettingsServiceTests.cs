using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证单个好友自主互动设置的默认值、账号边界和 SQLite 持久化。
/// </summary>
public sealed class AiAccountAutonomySettingsServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void TryGetSettings_ForExistingAccount_ReturnsDefaults()
    {
        AiAccount account = CreateAccount("FriendDefaults");
        AiAccountAutonomySettingsService service = CreateSettingsService();

        bool succeeded = service.TryGetSettings(
            account.Id,
            out AiAccountAutonomySettings? settings);

        Assert.True(succeeded);
        Assert.NotNull(settings);
        Assert.True(settings.IsEnabled);
        Assert.Equal(
            AutonomousInteractionInitiativeLevel.Normal,
            settings.InitiativeLevel);
        Assert.True(settings.CanInitiatePrivateChats);
        Assert.True(settings.CanInitiateGroupChats);
        Assert.True(settings.CanJoinGroupChats);
    }

    [Fact]
    public void TryUpdateSettings_PersistsForOnlyTheSelectedAccount()
    {
        AiAccount selectedAccount = CreateAccount("SelectedFriend");
        AiAccount otherAccount = CreateAccount("OtherFriend");
        AiAccountAutonomySettingsService service = CreateSettingsService();

        bool succeeded = service.TryUpdateSettings(
            selectedAccount.Id,
            isEnabled: true,
            AutonomousInteractionInitiativeLevel.High,
            canInitiatePrivateChats: false,
            canInitiateGroupChats: true,
            canJoinGroupChats: false,
            out AiAccountAutonomySettings? savedSettings);

        Assert.True(succeeded);
        Assert.NotNull(savedSettings);

        AiAccountAutonomySettingsService restartedService =
            CreateSettingsService();
        Assert.True(restartedService.TryGetSettings(
            selectedAccount.Id,
            out AiAccountAutonomySettings? reloadedSettings));
        Assert.Equal(
            AutonomousInteractionInitiativeLevel.High,
            reloadedSettings!.InitiativeLevel);
        Assert.False(reloadedSettings.CanInitiatePrivateChats);
        Assert.False(reloadedSettings.CanJoinGroupChats);

        Assert.True(restartedService.TryGetSettings(
            otherAccount.Id,
            out AiAccountAutonomySettings? otherSettings));
        Assert.Equal(
            AutonomousInteractionInitiativeLevel.Normal,
            otherSettings!.InitiativeLevel);
    }

    [Fact]
    public void MissingAccount_CannotReadOrSaveSettings()
    {
        AiAccountAutonomySettingsService service = CreateSettingsService();
        Guid missingAccountId = Guid.NewGuid();

        Assert.False(service.TryGetSettings(
            missingAccountId,
            out AiAccountAutonomySettings? readSettings));
        Assert.Null(readSettings);

        Assert.False(service.TryUpdateSettings(
            missingAccountId,
            isEnabled: true,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiatePrivateChats: true,
            canInitiateGroupChats: true,
            canJoinGroupChats: true,
            out AiAccountAutonomySettings? savedSettings));
        Assert.Null(savedSettings);
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

    private AiAccountAutonomySettingsService CreateSettingsService()
    {
        return new AiAccountAutonomySettingsService(
            _database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

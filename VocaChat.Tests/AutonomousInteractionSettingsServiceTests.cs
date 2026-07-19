using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证好友自主互动全局设置的默认值、验证规则和 SQLite 持久化。
/// </summary>
public sealed class AutonomousInteractionSettingsServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void GetSettings_BeforeFirstSave_ReturnsSafeDefaults()
    {
        AutonomousInteractionSettingsService service = CreateService();

        AutonomousInteractionSettings settings = service.GetSettings();

        Assert.False(settings.IsEnabled);
        Assert.Equal(AutonomousInteractionFrequency.Normal, settings.Frequency);
        Assert.True(settings.AllowPrivateChats);
        Assert.True(settings.AllowGroupChats);
        Assert.Equal(80, settings.PrivateChatContinuationRatePercent);
        Assert.Equal(6, settings.PrivateChatMaximumRounds);
    }

    [Fact]
    public void TryUpdateSettings_PersistsAcrossServiceInstances()
    {
        AutonomousInteractionSettingsService service = CreateService();

        bool succeeded = service.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.High,
            allowPrivateChats: true,
            allowGroupChats: false,
            privateChatContinuationRatePercent: 65,
            privateChatMaximumRounds: 9,
            out AutonomousInteractionSettings? savedSettings,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(savedSettings);

        AutonomousInteractionSettings reloadedSettings =
            CreateService().GetSettings();

        Assert.True(reloadedSettings.IsEnabled);
        Assert.Equal(
            AutonomousInteractionFrequency.High,
            reloadedSettings.Frequency);
        Assert.True(reloadedSettings.AllowPrivateChats);
        Assert.False(reloadedSettings.AllowGroupChats);
        Assert.Equal(65, reloadedSettings.PrivateChatContinuationRatePercent);
        Assert.Equal(9, reloadedSettings.PrivateChatMaximumRounds);
    }

    [Fact]
    public void TryUpdateSettings_WithUnknownFrequency_DoesNotSave()
    {
        AutonomousInteractionSettingsService service = CreateService();

        bool succeeded = service.TryUpdateSettings(
            isEnabled: true,
            (AutonomousInteractionFrequency)999,
            allowPrivateChats: false,
            allowGroupChats: false,
            privateChatContinuationRatePercent: 80,
            privateChatMaximumRounds: 6,
            out AutonomousInteractionSettings? settings,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(settings);
        Assert.Equal("自主互动频率无效。", errorMessage);
        Assert.False(service.GetSettings().IsEnabled);
    }

    [Theory]
    [InlineData(-1, 6)]
    [InlineData(96, 6)]
    [InlineData(80, 0)]
    [InlineData(80, 13)]
    public void TryUpdateSettings_WithInvalidConversationLimits_DoesNotSave(
        int continuationRatePercent,
        int maximumRounds)
    {
        AutonomousInteractionSettingsService service = CreateService();

        bool succeeded = service.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.Normal,
            allowPrivateChats: true,
            allowGroupChats: false,
            continuationRatePercent,
            maximumRounds,
            out AutonomousInteractionSettings? settings,
            out _);

        Assert.False(succeeded);
        Assert.Null(settings);
        Assert.False(service.GetSettings().IsEnabled);
    }

    private AutonomousInteractionSettingsService CreateService()
    {
        return new AutonomousInteractionSettingsService(
            _database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

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
        Assert.Equal(6, settings.AutonomousGroupChatMaximumMembers);
        Assert.Equal(80, settings.GroupChatContinuationRatePercent);
        Assert.Equal(4, settings.GroupChatMaximumRounds);
        Assert.Equal(2, settings.GroupChatMaximumSpeakersPerTurn);
        Assert.Equal(3, settings.GroupChatWholeGroupMaximumSpeakersPerTurn);
        Assert.Equal(6, settings.GroupChatMaximumMessagesPerTurn);
    }

    [Fact]
    public void TryUpdateSettings_PersistsGroupChatDensityAcrossServiceInstances()
    {
        AutonomousInteractionSettingsService service = CreateService();

        bool succeeded = TryUpdateGroupChatDensity(
            service,
            maximumSpeakers: 3,
            wholeGroupMaximumSpeakers: 5,
            maximumMessages: 8,
            out AutonomousInteractionSettings? savedSettings,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(savedSettings);

        AutonomousInteractionSettings reloaded = CreateService().GetSettings();

        Assert.Equal(3, reloaded.GroupChatMaximumSpeakersPerTurn);
        Assert.Equal(5, reloaded.GroupChatWholeGroupMaximumSpeakersPerTurn);
        Assert.Equal(8, reloaded.GroupChatMaximumMessagesPerTurn);
    }

    [Theory]
    [InlineData(0, 3, 6)]
    [InlineData(2, 13, 13)]
    [InlineData(4, 3, 3)]
    [InlineData(2, 5, 4)]
    public void TryUpdateSettings_WithInvalidGroupChatDensity_DoesNotSave(
        int maximumSpeakers,
        int wholeGroupMaximumSpeakers,
        int maximumMessages)
    {
        AutonomousInteractionSettingsService service = CreateService();

        bool succeeded = TryUpdateGroupChatDensity(
            service,
            maximumSpeakers,
            wholeGroupMaximumSpeakers,
            maximumMessages,
            out AutonomousInteractionSettings? settings,
            out _);

        Assert.False(succeeded);
        Assert.Null(settings);
        Assert.False(service.GetSettings().IsEnabled);
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
            autonomousGroupChatMaximumMembers: 14,
            groupChatContinuationRatePercent: 72,
            groupChatMaximumRounds: 7,
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
        Assert.Equal(14, reloadedSettings.AutonomousGroupChatMaximumMembers);
        Assert.Equal(72, reloadedSettings.GroupChatContinuationRatePercent);
        Assert.Equal(7, reloadedSettings.GroupChatMaximumRounds);
    }

    [Theory]
    [InlineData(-1, 4)]
    [InlineData(96, 4)]
    [InlineData(80, 0)]
    [InlineData(80, 13)]
    public void TryUpdateSettings_WithInvalidGroupConversationLimits_DoesNotSave(
        int continuationRatePercent,
        int maximumRounds)
    {
        AutonomousInteractionSettingsService service = CreateService();

        bool succeeded = service.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.Normal,
            allowPrivateChats: true,
            allowGroupChats: true,
            privateChatContinuationRatePercent: 80,
            privateChatMaximumRounds: 6,
            autonomousGroupChatMaximumMembers: 6,
            groupChatContinuationRatePercent: continuationRatePercent,
            groupChatMaximumRounds: maximumRounds,
            out AutonomousInteractionSettings? settings,
            out _);

        Assert.False(succeeded);
        Assert.Null(settings);
        Assert.False(service.GetSettings().IsEnabled);
    }

    [Fact]
    public void TryUpdateSettings_WithGroupMaximumBelowThree_DoesNotSave()
    {
        AutonomousInteractionSettingsService service = CreateService();

        bool succeeded = service.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.Normal,
            allowPrivateChats: true,
            allowGroupChats: true,
            privateChatContinuationRatePercent: 80,
            privateChatMaximumRounds: 6,
            autonomousGroupChatMaximumMembers: 2,
            out AutonomousInteractionSettings? settings,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(settings);
        Assert.Equal("自主好友群聊至少需要允许 3 名好友。", errorMessage);
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

    private static bool TryUpdateGroupChatDensity(
        AutonomousInteractionSettingsService service,
        int maximumSpeakers,
        int wholeGroupMaximumSpeakers,
        int maximumMessages,
        out AutonomousInteractionSettings? settings,
        out string errorMessage)
    {
        return service.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.Normal,
            allowPrivateChats: true,
            allowGroupChats: true,
            privateChatContinuationRatePercent: 80,
            privateChatMaximumRounds: 6,
            autonomousGroupChatMaximumMembers: 6,
            groupChatContinuationRatePercent: 80,
            groupChatMaximumRounds: 4,
            AiReplyDelayMode.RandomRange,
            fixedReplyDelayMilliseconds: 1200,
            minimumReplyDelayMilliseconds: 800,
            maximumReplyDelayMilliseconds: 1800,
            AiReplyDelayMode.RandomRange,
            fixedConsecutiveMessageDelayMilliseconds: 700,
            minimumConsecutiveMessageDelayMilliseconds: 400,
            maximumConsecutiveMessageDelayMilliseconds: 1200,
            maximumConsecutiveQuestionTurns: 2,
            minimumReplyMessageCount: 1,
            maximumReplyMessageCount: 4,
            groupChatMaximumSpeakersPerTurn: maximumSpeakers,
            groupChatWholeGroupMaximumSpeakersPerTurn:
                wholeGroupMaximumSpeakers,
            groupChatMaximumMessagesPerTurn: maximumMessages,
            out settings,
            out errorMessage);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

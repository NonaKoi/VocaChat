using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证单次回复条数的全局默认、持久化和好友专有覆盖规则。
/// </summary>
public sealed class AiReplyMessageCountSettingsResolverTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void Resolve_WithoutSavedSettings_ReturnsOneToFour()
    {
        AiAccount account = CreateAccount("DefaultReplyCount");

        AiMessageCountRange range = CreateResolver().Resolve(account.Id);

        Assert.Equal(1, range.Minimum);
        Assert.Equal(4, range.Maximum);
    }

    [Fact]
    public void Resolve_AfterGlobalSettingsSaved_ReturnsPersistedRange()
    {
        AiAccount account = CreateAccount("GlobalReplyCount");
        SaveGlobalRange(2, 3);

        AiMessageCountRange range = CreateResolver().Resolve(account.Id);

        Assert.Equal(2, range.Minimum);
        Assert.Equal(3, range.Maximum);
    }

    [Fact]
    public void Resolve_AccountOverrideWinsWithoutChangingOtherAccounts()
    {
        AiAccount overriddenAccount = CreateAccount("OverrideReplyCount");
        AiAccount globalAccount = CreateAccount("InheritedReplyCount");
        SaveGlobalRange(2, 3);

        AiAccountAutonomySettingsService service = new(
            _database.CreateDbContextFactory());
        bool saved = service.TryUpdateSettings(
            overriddenAccount.Id,
            isEnabled: true,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiatePrivateChats: true,
            canInitiateGroupChats: true,
            canJoinGroupChats: true,
            useGlobalReplyDelay: true,
            AiReplyDelayMode.RandomRange,
            fixedReplyDelayMilliseconds: 1200,
            minimumReplyDelayMilliseconds: 800,
            maximumReplyDelayMilliseconds: 1800,
            useGlobalConsecutiveMessageDelay: true,
            AiReplyDelayMode.RandomRange,
            fixedConsecutiveMessageDelayMilliseconds: 700,
            minimumConsecutiveMessageDelayMilliseconds: 400,
            maximumConsecutiveMessageDelayMilliseconds: 1200,
            useGlobalQuestionPolicy: true,
            maximumConsecutiveQuestionTurns: 2,
            useGlobalReplyMessageCount: false,
            minimumReplyMessageCount: 1,
            maximumReplyMessageCount: 4,
            out AiAccountAutonomySettings? settings,
            out string errorMessage);

        Assert.True(saved, errorMessage);
        Assert.NotNull(settings);
        Assert.Equal(1, CreateResolver().Resolve(overriddenAccount.Id).Minimum);
        Assert.Equal(4, CreateResolver().Resolve(overriddenAccount.Id).Maximum);
        Assert.Equal(2, CreateResolver().Resolve(globalAccount.Id).Minimum);
        Assert.Equal(3, CreateResolver().Resolve(globalAccount.Id).Maximum);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 5)]
    [InlineData(4, 2)]
    public void GlobalSettings_InvalidReplyCountRange_IsRejected(
        int minimum,
        int maximum)
    {
        Assert.False(TrySaveGlobalRange(minimum, maximum, out _));
    }

    private AiReplyMessageCountSettingsResolver CreateResolver() =>
        new(_database.CreateDbContextFactory());

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private void SaveGlobalRange(int minimum, int maximum)
    {
        Assert.True(
            TrySaveGlobalRange(minimum, maximum, out string errorMessage),
            errorMessage);
    }

    private bool TrySaveGlobalRange(
        int minimum,
        int maximum,
        out string errorMessage)
    {
        AutonomousInteractionSettingsService service = new(
            _database.CreateDbContextFactory());
        return service.TryUpdateSettings(
            isEnabled: false,
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
            minimumReplyMessageCount: minimum,
            maximumReplyMessageCount: maximum,
            out _,
            out errorMessage);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

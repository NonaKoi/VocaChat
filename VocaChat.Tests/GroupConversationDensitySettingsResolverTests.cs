using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证群聊发言人数和消息总量设置的读取与适用范围判断。
/// </summary>
public sealed class GroupConversationDensitySettingsResolverTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void Resolve_BeforeFirstSave_ReturnsSafeDefaults()
    {
        GroupConversationDensitySettings settings = CreateResolver().Resolve();

        Assert.Equal(2, settings.MaximumSpeakersPerTurn);
        Assert.Equal(3, settings.WholeGroupMaximumSpeakersPerTurn);
        Assert.Equal(6, settings.MaximumMessagesPerTurn);
    }

    [Fact]
    public void Resolve_AfterSave_ReturnsPersistedDensity()
    {
        SaveDensity(maximumSpeakers: 3, wholeGroupMaximumSpeakers: 5,
            maximumMessages: 8);

        GroupConversationDensitySettings settings = CreateResolver().Resolve();

        Assert.Equal(3, settings.MaximumSpeakersPerTurn);
        Assert.Equal(5, settings.WholeGroupMaximumSpeakersPerTurn);
        Assert.Equal(8, settings.MaximumMessagesPerTurn);
    }

    [Theory]
    [InlineData("你怎么看这个安排？", 2)]
    [InlineData("大家怎么看这个安排？", 3)]
    [InlineData("@Alpha 和 @Beta 分别说说", 3)]
    public void ResolveMaximumSpeakerCount_DistinguishesNormalAndWholeGroupMessages(
        string content,
        int expectedMaximumSpeakers)
    {
        GroupChat groupChat = new("测试群");
        groupChat.AddMember(CreateAccount("Alpha"));
        groupChat.AddMember(CreateAccount("Beta"));
        groupChat.AddMember(CreateAccount("Gamma"));
        GroupConversationDensitySettings settings = new(2, 3, 6);

        int maximumSpeakers = settings.ResolveMaximumSpeakerCount(
            groupChat,
            content);

        Assert.Equal(expectedMaximumSpeakers, maximumSpeakers);
    }

    private GroupConversationDensitySettingsResolver CreateResolver()
    {
        return new GroupConversationDensitySettingsResolver(
            _database.CreateDbContextFactory());
    }

    private void SaveDensity(
        int maximumSpeakers,
        int wholeGroupMaximumSpeakers,
        int maximumMessages)
    {
        AutonomousInteractionSettingsService service = new(
            _database.CreateDbContextFactory());
        bool succeeded = service.TryUpdateSettings(
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
            out _,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
    }

    private static AiAccount CreateAccount(string nickname)
    {
        return new AiAccount(
            $"{nickname}-vc",
            nickname,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证全局与好友专有回复速度设置，以及模型耗时计入等待间隔的规则。
/// </summary>
public sealed class AiReplyTimingSchedulerTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void GlobalRandomRange_AcceptsUserDefinedRangeWithoutProductMaximum()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AutonomousInteractionSettingsService service = new(factory);

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
            minimumReplyDelayMilliseconds: 0,
            maximumReplyDelayMilliseconds: long.MaxValue,
            out AutonomousInteractionSettings? settings,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.Equal(long.MaxValue, settings!.MaximumReplyDelayMilliseconds);
    }

    [Fact]
    public void AccountOverride_WithEqualRandomBounds_UsesTheConfiguredBound()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory);
        AiAccountAutonomySettingsService service = new(factory);
        Assert.True(service.TryUpdateSettings(
            account.Id,
            isEnabled: true,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiatePrivateChats: true,
            canInitiateGroupChats: true,
            canJoinGroupChats: true,
            useGlobalReplyDelay: false,
            AiReplyDelayMode.RandomRange,
            fixedReplyDelayMilliseconds: 999,
            minimumReplyDelayMilliseconds: 777,
            maximumReplyDelayMilliseconds: 777,
            out _,
            out string errorMessage), errorMessage);

        long delay = new AiReplyTimingScheduler(factory)
            .ResolveDelayMilliseconds(account.Id);

        Assert.Equal(777, delay);
    }

    [Fact]
    public async Task WaitForReply_ModelGenerationTimeReducesRemainingDelay()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory);
        AutonomousInteractionSettingsService service = new(factory);
        Assert.True(service.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.Normal,
            allowPrivateChats: true,
            allowGroupChats: true,
            privateChatContinuationRatePercent: 80,
            privateChatMaximumRounds: 6,
            autonomousGroupChatMaximumMembers: 6,
            groupChatContinuationRatePercent: 80,
            groupChatMaximumRounds: 4,
            AiReplyDelayMode.Fixed,
            fixedReplyDelayMilliseconds: 1000,
            minimumReplyDelayMilliseconds: 0,
            maximumReplyDelayMilliseconds: 0,
            out _,
            out string errorMessage), errorMessage);
        long capturedDelay = -1;
        AiReplyTimingScheduler scheduler = new(
            factory,
            (delay, _) =>
            {
                capturedDelay = delay;
                return Task.CompletedTask;
            });

        await scheduler.WaitForReplyAsync(
            account.Id,
            DateTime.Now.AddMilliseconds(-400));

        // 创建 DbContext 和读取设置的耗时会随并行测试负载变化；这里只验证
        // 已经过的 400ms 确实从配置间隔中扣除，而不依赖数据库查询必须在固定时间内完成。
        Assert.InRange(capturedDelay, 0, 700);
        Assert.True(capturedDelay < 1000);
    }

    [Fact]
    public void InvalidRandomRange_IsRejectedWithoutSaving()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AutonomousInteractionSettingsService service = new(factory);

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
            fixedReplyDelayMilliseconds: 1000,
            minimumReplyDelayMilliseconds: 2000,
            maximumReplyDelayMilliseconds: 1000,
            out _,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Contains("最小值", errorMessage);
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private static AiAccount CreateAccount(VocaChatDbContextFactory factory)
    {
        AiAccountService service = new(factory);
        Assert.True(service.TryCreateAiAccount(
            "回复速度测试好友",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return Assert.IsType<AiAccount>(account);
    }
}

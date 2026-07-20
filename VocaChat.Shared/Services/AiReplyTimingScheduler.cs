using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 根据全局设置和好友专有设置计算 AI 消息间隔，并等待到消息可以发送的时间。
/// </summary>
public sealed class AiReplyTimingScheduler
{
    private const long MaximumSingleDelayMilliseconds = int.MaxValue;

    private readonly VocaChatDbContextFactory _dbContextFactory;
    private readonly Func<long, CancellationToken, Task> _delayAsync;

    public AiReplyTimingScheduler(VocaChatDbContextFactory dbContextFactory)
        : this(dbContextFactory, DelayAsync)
    {
    }

    internal AiReplyTimingScheduler(
        VocaChatDbContextFactory dbContextFactory,
        Func<long, CancellationToken, Task> delayAsync)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _delayAsync = delayAsync
            ?? throw new ArgumentNullException(nameof(delayAsync));
    }

    /// <summary>
    /// 返回指定好友当前实际生效的回复间隔；专有设置未启用时使用全局设置。
    /// </summary>
    public long ResolveDelayMilliseconds(Guid aiAccountId)
    {
        return ResolveDelaySettings(aiAccountId, useConsecutiveMessageDelay: false);
    }

    /// <summary>
    /// 返回同一个好友一次表达拆成多条消息时使用的连续消息间隔。
    /// </summary>
    public long ResolveConsecutiveMessageDelayMilliseconds(Guid aiAccountId)
    {
        return ResolveDelaySettings(aiAccountId, useConsecutiveMessageDelay: true);
    }

    private long ResolveDelaySettings(
        Guid aiAccountId,
        bool useConsecutiveMessageDelay)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        AutonomousInteractionSettings globalSettings = dbContext
            .AutonomousInteractionSettings
            .AsNoTracking()
            .SingleOrDefault(settings =>
                settings.Id == AutonomousInteractionSettings.SingletonId)
            ?? new AutonomousInteractionSettings();
        AiAccountAutonomySettings? accountSettings = dbContext
            .AiAccountAutonomySettings
            .AsNoTracking()
            .SingleOrDefault(settings =>
                settings.AiAccountId == aiAccountId);

        bool useGlobal = accountSettings is null
            || (useConsecutiveMessageDelay
                ? accountSettings.UseGlobalConsecutiveMessageDelay
                : accountSettings.UseGlobalReplyDelay);

        if (useGlobal)
        {
            return SelectDelay(
                useConsecutiveMessageDelay
                    ? globalSettings.ConsecutiveMessageDelayMode
                    : globalSettings.ReplyDelayMode,
                useConsecutiveMessageDelay
                    ? globalSettings.FixedConsecutiveMessageDelayMilliseconds
                    : globalSettings.FixedReplyDelayMilliseconds,
                useConsecutiveMessageDelay
                    ? globalSettings.MinimumConsecutiveMessageDelayMilliseconds
                    : globalSettings.MinimumReplyDelayMilliseconds,
                useConsecutiveMessageDelay
                    ? globalSettings.MaximumConsecutiveMessageDelayMilliseconds
                    : globalSettings.MaximumReplyDelayMilliseconds);
        }

        return SelectDelay(
            useConsecutiveMessageDelay
                ? accountSettings!.ConsecutiveMessageDelayMode
                : accountSettings!.ReplyDelayMode,
            useConsecutiveMessageDelay
                ? accountSettings.FixedConsecutiveMessageDelayMilliseconds
                : accountSettings.FixedReplyDelayMilliseconds,
            useConsecutiveMessageDelay
                ? accountSettings.MinimumConsecutiveMessageDelayMilliseconds
                : accountSettings.MinimumReplyDelayMilliseconds,
            useConsecutiveMessageDelay
                ? accountSettings.MaximumConsecutiveMessageDelayMilliseconds
                : accountSettings.MaximumReplyDelayMilliseconds);
    }

    /// <summary>
    /// 将模型生成耗时计入间隔，只等待上一条消息之后尚未经过的剩余时间。
    /// </summary>
    public async Task WaitForReplyAsync(
        Guid aiAccountId,
        DateTime previousMessageSentAt,
        CancellationToken cancellationToken = default)
    {
        await WaitForConfiguredDelayAsync(
            ResolveDelayMilliseconds(aiAccountId),
            previousMessageSentAt,
            cancellationToken);
    }

    /// <summary>
    /// 等待同一个好友一次表达中的下一条消息可以发送。
    /// </summary>
    public async Task WaitForConsecutiveMessageAsync(
        Guid aiAccountId,
        DateTime previousMessageSentAt,
        CancellationToken cancellationToken = default)
    {
        await WaitForConfiguredDelayAsync(
            ResolveConsecutiveMessageDelayMilliseconds(aiAccountId),
            previousMessageSentAt,
            cancellationToken);
    }

    private async Task WaitForConfiguredDelayAsync(
        long configuredDelay,
        DateTime previousMessageSentAt,
        CancellationToken cancellationToken)
    {
        double elapsedMilliseconds = Math.Max(
            0,
            (DateTime.Now - previousMessageSentAt).TotalMilliseconds);
        long remainingDelay = configuredDelay <= elapsedMilliseconds
            ? 0
            : configuredDelay - (long)elapsedMilliseconds;

        await _delayAsync(remainingDelay, cancellationToken);
    }

    private static long SelectDelay(
        AiReplyDelayMode mode,
        long fixedDelay,
        long minimumDelay,
        long maximumDelay)
    {
        if (mode == AiReplyDelayMode.Fixed)
        {
            return fixedDelay;
        }

        if (minimumDelay == maximumDelay)
        {
            return minimumDelay;
        }

        if (maximumDelay == long.MaxValue)
        {
            double position = Random.Shared.NextDouble();
            decimal span = (decimal)maximumDelay - minimumDelay;
            return minimumDelay + (long)(span * (decimal)position);
        }

        return Random.Shared.NextInt64(minimumDelay, maximumDelay + 1);
    }

    private static async Task DelayAsync(
        long delayMilliseconds,
        CancellationToken cancellationToken)
    {
        long remainingDelay = Math.Max(0, delayMilliseconds);

        while (remainingDelay > 0)
        {
            int currentDelay = (int)Math.Min(
                remainingDelay,
                MaximumSingleDelayMilliseconds);
            await Task.Delay(currentDelay, cancellationToken);
            remainingDelay -= currentDelay;
        }
    }
}

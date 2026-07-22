using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 解析指定 AI 账号实际生效的单次回复消息条数范围。
/// </summary>
public sealed class AiReplyMessageCountSettingsResolver
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiReplyMessageCountSettingsResolver(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public AiMessageCountRange Resolve(Guid aiAccountId)
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

        if (accountSettings is null
            || accountSettings.UseGlobalReplyMessageCount)
        {
            return new AiMessageCountRange(
                globalSettings.MinimumReplyMessageCount,
                globalSettings.MaximumReplyMessageCount);
        }

        return new AiMessageCountRange(
            accountSettings.MinimumReplyMessageCount,
            accountSettings.MaximumReplyMessageCount);
    }
}

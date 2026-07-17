using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责单个 AI 账号自主互动设置的读取、验证和数据库保存。
/// </summary>
public class AiAccountAutonomySettingsService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiAccountAutonomySettingsService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 返回已有账号的专有设置；尚未保存过时返回安全默认值。
    /// </summary>
    public bool TryGetSettings(
        Guid aiAccountId,
        out AiAccountAutonomySettings? settings)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.AiAccounts.Any(account => account.Id == aiAccountId))
        {
            settings = null;
            return false;
        }

        settings = dbContext.AiAccountAutonomySettings
            .AsNoTracking()
            .SingleOrDefault(item => item.AiAccountId == aiAccountId)
            ?? new AiAccountAutonomySettings(aiAccountId);
        return true;
    }

    /// <summary>
    /// 验证并保存已有账号的专有设置。
    /// </summary>
    public bool TryUpdateSettings(
        Guid aiAccountId,
        bool isEnabled,
        AutonomousInteractionInitiativeLevel initiativeLevel,
        bool canInitiatePrivateChats,
        bool canInitiateGroupChats,
        bool canJoinGroupChats,
        out AiAccountAutonomySettings? settings)
    {
        settings = null;

        if (!Enum.IsDefined(initiativeLevel))
        {
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.AiAccounts.Any(account => account.Id == aiAccountId))
        {
            return false;
        }

        AiAccountAutonomySettings storedSettings =
            dbContext.AiAccountAutonomySettings.SingleOrDefault(item =>
                item.AiAccountId == aiAccountId)
            ?? new AiAccountAutonomySettings(aiAccountId);

        storedSettings.Update(
            isEnabled,
            initiativeLevel,
            canInitiatePrivateChats,
            canInitiateGroupChats,
            canJoinGroupChats);

        if (dbContext.Entry(storedSettings).State == EntityState.Detached)
        {
            dbContext.AiAccountAutonomySettings.Add(storedSettings);
        }

        dbContext.SaveChanges();
        settings = storedSettings;
        return true;
    }
}

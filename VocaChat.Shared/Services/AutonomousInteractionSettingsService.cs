using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责好友自主互动全局设置的读取、验证和数据库保存。
/// </summary>
public class AutonomousInteractionSettingsService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousInteractionSettingsService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 返回已经保存的设置；首次使用时返回尚未写入数据库的安全默认值。
    /// </summary>
    public AutonomousInteractionSettings GetSettings()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.AutonomousInteractionSettings
            .AsNoTracking()
            .SingleOrDefault(settings =>
                settings.Id == AutonomousInteractionSettings.SingletonId)
            ?? new AutonomousInteractionSettings();
    }

    /// <summary>
    /// 验证并保存全局设置；当前项目始终只维护固定 Id 的一行数据。
    /// </summary>
    public bool TryUpdateSettings(
        bool isEnabled,
        AutonomousInteractionFrequency frequency,
        bool allowPrivateChats,
        bool allowGroupChats,
        out AutonomousInteractionSettings? settings,
        out string errorMessage)
    {
        settings = null;

        if (!Enum.IsDefined(frequency))
        {
            errorMessage = "自主互动频率无效。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        AutonomousInteractionSettings storedSettings =
            dbContext.AutonomousInteractionSettings.SingleOrDefault(item =>
                item.Id == AutonomousInteractionSettings.SingletonId)
            ?? new AutonomousInteractionSettings();

        storedSettings.Update(
            isEnabled,
            frequency,
            allowPrivateChats,
            allowGroupChats);

        if (dbContext.Entry(storedSettings).State == EntityState.Detached)
        {
            dbContext.AutonomousInteractionSettings.Add(storedSettings);
        }

        dbContext.SaveChanges();

        settings = storedSettings;
        errorMessage = string.Empty;
        return true;
    }
}

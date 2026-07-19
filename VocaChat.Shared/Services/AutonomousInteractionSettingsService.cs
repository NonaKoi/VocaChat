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
        int privateChatContinuationRatePercent,
        int privateChatMaximumRounds,
        out AutonomousInteractionSettings? settings,
        out string errorMessage)
    {
        return TryUpdateSettings(
            isEnabled,
            frequency,
            allowPrivateChats,
            allowGroupChats,
            privateChatContinuationRatePercent,
            privateChatMaximumRounds,
            AutonomousInteractionSettings
                .DefaultAutonomousGroupChatMaximumMembers,
            out settings,
            out errorMessage);
    }

    /// <summary>
    /// 验证并保存包含自主群聊成员上限的完整全局设置。
    /// </summary>
    public bool TryUpdateSettings(
        bool isEnabled,
        AutonomousInteractionFrequency frequency,
        bool allowPrivateChats,
        bool allowGroupChats,
        int privateChatContinuationRatePercent,
        int privateChatMaximumRounds,
        int autonomousGroupChatMaximumMembers,
        out AutonomousInteractionSettings? settings,
        out string errorMessage)
    {
        settings = null;

        if (!Enum.IsDefined(frequency))
        {
            errorMessage = "自主互动频率无效。";
            return false;
        }

        if (privateChatContinuationRatePercent
                is < AutonomousInteractionSettings.MinimumPrivateChatContinuationRatePercent
                or > AutonomousInteractionSettings.MaximumPrivateChatContinuationRatePercent)
        {
            errorMessage = "下一轮概率保留比例必须在 0% 到 95% 之间。";
            return false;
        }

        if (privateChatMaximumRounds
                is < AutonomousInteractionSettings.MinimumPrivateChatMaximumRounds
                or > AutonomousInteractionSettings.MaximumPrivateChatMaximumRounds)
        {
            errorMessage = "单次好友私信最大轮数必须在 1 到 12 之间。";
            return false;
        }

        if (autonomousGroupChatMaximumMembers
                < AutonomousInteractionSettings
                    .MinimumAutonomousGroupChatMaximumMembers)
        {
            errorMessage = "自主好友群聊至少需要允许 3 名好友。";
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
            allowGroupChats,
            privateChatContinuationRatePercent,
            privateChatMaximumRounds,
            autonomousGroupChatMaximumMembers);

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

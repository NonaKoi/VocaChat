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
            AutonomousInteractionSettings
                .DefaultGroupChatContinuationRatePercent,
            AutonomousInteractionSettings.DefaultGroupChatMaximumRounds,
            AutonomousInteractionSettings.DefaultReplyDelayMode,
            AutonomousInteractionSettings.DefaultFixedReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultConsecutiveMessageDelayMode,
            AutonomousInteractionSettings.DefaultFixedConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveQuestionTurns,
            AutonomousInteractionSettings.DefaultMinimumReplyMessageCount,
            AutonomousInteractionSettings.DefaultMaximumReplyMessageCount,
            AutonomousInteractionSettings.DefaultGroupChatMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatWholeGroupMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatMaximumMessagesPerTurn,
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
        return TryUpdateSettings(
            isEnabled,
            frequency,
            allowPrivateChats,
            allowGroupChats,
            privateChatContinuationRatePercent,
            privateChatMaximumRounds,
            autonomousGroupChatMaximumMembers,
            AutonomousInteractionSettings
                .DefaultGroupChatContinuationRatePercent,
            AutonomousInteractionSettings.DefaultGroupChatMaximumRounds,
            AutonomousInteractionSettings.DefaultReplyDelayMode,
            AutonomousInteractionSettings.DefaultFixedReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultConsecutiveMessageDelayMode,
            AutonomousInteractionSettings.DefaultFixedConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveQuestionTurns,
            AutonomousInteractionSettings.DefaultMinimumReplyMessageCount,
            AutonomousInteractionSettings.DefaultMaximumReplyMessageCount,
            AutonomousInteractionSettings.DefaultGroupChatMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatWholeGroupMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatMaximumMessagesPerTurn,
            out settings,
            out errorMessage);
    }

    /// <summary>
    /// 验证并保存包含私信和好友群聊轮次限制的完整全局设置。
    /// </summary>
    public bool TryUpdateSettings(
        bool isEnabled,
        AutonomousInteractionFrequency frequency,
        bool allowPrivateChats,
        bool allowGroupChats,
        int privateChatContinuationRatePercent,
        int privateChatMaximumRounds,
        int autonomousGroupChatMaximumMembers,
        int groupChatContinuationRatePercent,
        int groupChatMaximumRounds,
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
            autonomousGroupChatMaximumMembers,
            groupChatContinuationRatePercent,
            groupChatMaximumRounds,
            AutonomousInteractionSettings.DefaultReplyDelayMode,
            AutonomousInteractionSettings.DefaultFixedReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultConsecutiveMessageDelayMode,
            AutonomousInteractionSettings.DefaultFixedConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveQuestionTurns,
            AutonomousInteractionSettings.DefaultMinimumReplyMessageCount,
            AutonomousInteractionSettings.DefaultMaximumReplyMessageCount,
            AutonomousInteractionSettings.DefaultGroupChatMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatWholeGroupMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatMaximumMessagesPerTurn,
            out settings,
            out errorMessage);
    }

    /// <summary>
    /// 验证并保存包含回复速度调度在内的完整全局设置。
    /// </summary>
    public bool TryUpdateSettings(
        bool isEnabled,
        AutonomousInteractionFrequency frequency,
        bool allowPrivateChats,
        bool allowGroupChats,
        int privateChatContinuationRatePercent,
        int privateChatMaximumRounds,
        int autonomousGroupChatMaximumMembers,
        int groupChatContinuationRatePercent,
        int groupChatMaximumRounds,
        AiReplyDelayMode replyDelayMode,
        long fixedReplyDelayMilliseconds,
        long minimumReplyDelayMilliseconds,
        long maximumReplyDelayMilliseconds,
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
            autonomousGroupChatMaximumMembers,
            groupChatContinuationRatePercent,
            groupChatMaximumRounds,
            replyDelayMode,
            fixedReplyDelayMilliseconds,
            minimumReplyDelayMilliseconds,
            maximumReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultConsecutiveMessageDelayMode,
            AutonomousInteractionSettings.DefaultFixedConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveQuestionTurns,
            AutonomousInteractionSettings.DefaultMinimumReplyMessageCount,
            AutonomousInteractionSettings.DefaultMaximumReplyMessageCount,
            AutonomousInteractionSettings.DefaultGroupChatMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatWholeGroupMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatMaximumMessagesPerTurn,
            out settings,
            out errorMessage);
    }

    public bool TryUpdateSettings(
        bool isEnabled,
        AutonomousInteractionFrequency frequency,
        bool allowPrivateChats,
        bool allowGroupChats,
        int privateChatContinuationRatePercent,
        int privateChatMaximumRounds,
        int autonomousGroupChatMaximumMembers,
        int groupChatContinuationRatePercent,
        int groupChatMaximumRounds,
        AiReplyDelayMode replyDelayMode,
        long fixedReplyDelayMilliseconds,
        long minimumReplyDelayMilliseconds,
        long maximumReplyDelayMilliseconds,
        AiReplyDelayMode consecutiveMessageDelayMode,
        long fixedConsecutiveMessageDelayMilliseconds,
        long minimumConsecutiveMessageDelayMilliseconds,
        long maximumConsecutiveMessageDelayMilliseconds,
        int maximumConsecutiveQuestionTurns,
        int minimumReplyMessageCount,
        int maximumReplyMessageCount,
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
            autonomousGroupChatMaximumMembers,
            groupChatContinuationRatePercent,
            groupChatMaximumRounds,
            replyDelayMode,
            fixedReplyDelayMilliseconds,
            minimumReplyDelayMilliseconds,
            maximumReplyDelayMilliseconds,
            consecutiveMessageDelayMode,
            fixedConsecutiveMessageDelayMilliseconds,
            minimumConsecutiveMessageDelayMilliseconds,
            maximumConsecutiveMessageDelayMilliseconds,
            maximumConsecutiveQuestionTurns,
            minimumReplyMessageCount,
            maximumReplyMessageCount,
            AutonomousInteractionSettings.DefaultGroupChatMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatWholeGroupMaximumSpeakersPerTurn,
            AutonomousInteractionSettings.DefaultGroupChatMaximumMessagesPerTurn,
            out settings,
            out errorMessage);
    }

    public bool TryUpdateSettings(
        bool isEnabled,
        AutonomousInteractionFrequency frequency,
        bool allowPrivateChats,
        bool allowGroupChats,
        int privateChatContinuationRatePercent,
        int privateChatMaximumRounds,
        int autonomousGroupChatMaximumMembers,
        int groupChatContinuationRatePercent,
        int groupChatMaximumRounds,
        AiReplyDelayMode replyDelayMode,
        long fixedReplyDelayMilliseconds,
        long minimumReplyDelayMilliseconds,
        long maximumReplyDelayMilliseconds,
        AiReplyDelayMode consecutiveMessageDelayMode,
        long fixedConsecutiveMessageDelayMilliseconds,
        long minimumConsecutiveMessageDelayMilliseconds,
        long maximumConsecutiveMessageDelayMilliseconds,
        int maximumConsecutiveQuestionTurns,
        int minimumReplyMessageCount,
        int maximumReplyMessageCount,
        int groupChatMaximumSpeakersPerTurn,
        int groupChatWholeGroupMaximumSpeakersPerTurn,
        int groupChatMaximumMessagesPerTurn,
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

        if (groupChatContinuationRatePercent
                is < AutonomousInteractionSettings.MinimumGroupChatContinuationRatePercent
                or > AutonomousInteractionSettings.MaximumGroupChatContinuationRatePercent)
        {
            errorMessage = "好友群聊下一轮概率保留比例必须在 0% 到 95% 之间。";
            return false;
        }

        if (groupChatMaximumRounds
                is < AutonomousInteractionSettings.MinimumGroupChatMaximumRounds
                or > AutonomousInteractionSettings.MaximumGroupChatMaximumRounds)
        {
            errorMessage = "单次好友群聊最大轮数必须在 1 到 12 之间。";
            return false;
        }

        if (!TryValidateReplyDelay(
                replyDelayMode,
                fixedReplyDelayMilliseconds,
                minimumReplyDelayMilliseconds,
                maximumReplyDelayMilliseconds,
                out errorMessage))
        {
            return false;
        }

        if (!TryValidateReplyDelay(
                consecutiveMessageDelayMode,
                fixedConsecutiveMessageDelayMilliseconds,
                minimumConsecutiveMessageDelayMilliseconds,
                maximumConsecutiveMessageDelayMilliseconds,
                out errorMessage))
        {
            return false;
        }

        if (maximumConsecutiveQuestionTurns
            < AutonomousInteractionSettings.MinimumMaximumConsecutiveQuestionTurns)
        {
            errorMessage = "连续疑问轮次上限必须至少为 1。";
            return false;
        }

        if (!TryValidateReplyMessageCountRange(
                minimumReplyMessageCount,
                maximumReplyMessageCount,
                out errorMessage))
        {
            return false;
        }

        if (!TryValidateGroupChatDensity(
                groupChatMaximumSpeakersPerTurn,
                groupChatWholeGroupMaximumSpeakersPerTurn,
                groupChatMaximumMessagesPerTurn,
                out errorMessage))
        {
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
            autonomousGroupChatMaximumMembers,
            groupChatContinuationRatePercent,
            groupChatMaximumRounds,
            replyDelayMode,
            fixedReplyDelayMilliseconds,
            minimumReplyDelayMilliseconds,
            maximumReplyDelayMilliseconds,
            consecutiveMessageDelayMode,
            fixedConsecutiveMessageDelayMilliseconds,
            minimumConsecutiveMessageDelayMilliseconds,
            maximumConsecutiveMessageDelayMilliseconds,
            maximumConsecutiveQuestionTurns,
            minimumReplyMessageCount,
            maximumReplyMessageCount,
            groupChatMaximumSpeakersPerTurn,
            groupChatWholeGroupMaximumSpeakersPerTurn,
            groupChatMaximumMessagesPerTurn);

        if (dbContext.Entry(storedSettings).State == EntityState.Detached)
        {
            dbContext.AutonomousInteractionSettings.Add(storedSettings);
        }

        dbContext.SaveChanges();

        settings = storedSettings;
        errorMessage = string.Empty;
        return true;
    }

    internal static bool TryValidateReplyDelay(
        AiReplyDelayMode replyDelayMode,
        long fixedReplyDelayMilliseconds,
        long minimumReplyDelayMilliseconds,
        long maximumReplyDelayMilliseconds,
        out string errorMessage)
    {
        if (!Enum.IsDefined(replyDelayMode))
        {
            errorMessage = "回复速度模式无效。";
            return false;
        }

        if (fixedReplyDelayMilliseconds < 0
            || minimumReplyDelayMilliseconds < 0
            || maximumReplyDelayMilliseconds < 0)
        {
            errorMessage = "回复间隔不能为负数。";
            return false;
        }

        if (minimumReplyDelayMilliseconds > maximumReplyDelayMilliseconds)
        {
            errorMessage = "随机回复间隔的最小值不能大于最大值。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    internal static bool TryValidateReplyMessageCountRange(
        int minimumReplyMessageCount,
        int maximumReplyMessageCount,
        out string errorMessage)
    {
        if (minimumReplyMessageCount
                is < AutonomousInteractionSettings.MinimumAllowedReplyMessageCount
                or > AutonomousInteractionSettings.MaximumAllowedReplyMessageCount
            || maximumReplyMessageCount
                is < AutonomousInteractionSettings.MinimumAllowedReplyMessageCount
                or > AutonomousInteractionSettings.MaximumAllowedReplyMessageCount)
        {
            errorMessage = "单次回复消息条数必须在 1 到 4 之间。";
            return false;
        }

        if (minimumReplyMessageCount > maximumReplyMessageCount)
        {
            errorMessage = "单次回复消息条数的下限不能大于上限。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    internal static bool TryValidateGroupChatDensity(
        int groupChatMaximumSpeakersPerTurn,
        int groupChatWholeGroupMaximumSpeakersPerTurn,
        int groupChatMaximumMessagesPerTurn,
        out string errorMessage)
    {
        if (groupChatMaximumSpeakersPerTurn
                is < AutonomousInteractionSettings.MinimumGroupChatDensityLimit
                or > AutonomousInteractionSettings.MaximumGroupChatDensityLimit
            || groupChatWholeGroupMaximumSpeakersPerTurn
                is < AutonomousInteractionSettings.MinimumGroupChatDensityLimit
                or > AutonomousInteractionSettings.MaximumGroupChatDensityLimit
            || groupChatMaximumMessagesPerTurn
                is < AutonomousInteractionSettings.MinimumGroupChatDensityLimit
                or > AutonomousInteractionSettings.MaximumGroupChatDensityLimit)
        {
            errorMessage = "群聊单轮发言人数和消息总量必须在 1 到 12 之间。";
            return false;
        }

        if (groupChatMaximumSpeakersPerTurn > groupChatMaximumMessagesPerTurn
            || groupChatWholeGroupMaximumSpeakersPerTurn
                > groupChatMaximumMessagesPerTurn)
        {
            errorMessage = "群聊单轮消息总量不能小于允许发言的好友人数。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}

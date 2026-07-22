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
        return TryUpdateSettings(
            aiAccountId,
            isEnabled,
            initiativeLevel,
            canInitiatePrivateChats,
            canInitiateGroupChats,
            canJoinGroupChats,
            useGlobalReplyDelay: true,
            AutonomousInteractionSettings.DefaultReplyDelayMode,
            AutonomousInteractionSettings.DefaultFixedReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumReplyDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumReplyDelayMilliseconds,
            useGlobalConsecutiveMessageDelay: true,
            AutonomousInteractionSettings.DefaultConsecutiveMessageDelayMode,
            AutonomousInteractionSettings.DefaultFixedConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveMessageDelayMilliseconds,
            useGlobalQuestionPolicy: true,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveQuestionTurns,
            useGlobalReplyMessageCount: true,
            AutonomousInteractionSettings.DefaultMinimumReplyMessageCount,
            AutonomousInteractionSettings.DefaultMaximumReplyMessageCount,
            out settings,
            out _);
    }

    /// <summary>
    /// 验证并保存好友自主权限及其专有回复速度设置。
    /// </summary>
    public bool TryUpdateSettings(
        Guid aiAccountId,
        bool isEnabled,
        AutonomousInteractionInitiativeLevel initiativeLevel,
        bool canInitiatePrivateChats,
        bool canInitiateGroupChats,
        bool canJoinGroupChats,
        bool useGlobalReplyDelay,
        AiReplyDelayMode replyDelayMode,
        long fixedReplyDelayMilliseconds,
        long minimumReplyDelayMilliseconds,
        long maximumReplyDelayMilliseconds,
        out AiAccountAutonomySettings? settings,
        out string errorMessage)
    {
        return TryUpdateSettings(
            aiAccountId,
            isEnabled,
            initiativeLevel,
            canInitiatePrivateChats,
            canInitiateGroupChats,
            canJoinGroupChats,
            useGlobalReplyDelay,
            replyDelayMode,
            fixedReplyDelayMilliseconds,
            minimumReplyDelayMilliseconds,
            maximumReplyDelayMilliseconds,
            useGlobalConsecutiveMessageDelay: true,
            AutonomousInteractionSettings.DefaultConsecutiveMessageDelayMode,
            AutonomousInteractionSettings.DefaultFixedConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMinimumConsecutiveMessageDelayMilliseconds,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveMessageDelayMilliseconds,
            useGlobalQuestionPolicy: true,
            AutonomousInteractionSettings.DefaultMaximumConsecutiveQuestionTurns,
            useGlobalReplyMessageCount: true,
            AutonomousInteractionSettings.DefaultMinimumReplyMessageCount,
            AutonomousInteractionSettings.DefaultMaximumReplyMessageCount,
            out settings,
            out errorMessage);
    }

    public bool TryUpdateSettings(
        Guid aiAccountId,
        bool isEnabled,
        AutonomousInteractionInitiativeLevel initiativeLevel,
        bool canInitiatePrivateChats,
        bool canInitiateGroupChats,
        bool canJoinGroupChats,
        bool useGlobalReplyDelay,
        AiReplyDelayMode replyDelayMode,
        long fixedReplyDelayMilliseconds,
        long minimumReplyDelayMilliseconds,
        long maximumReplyDelayMilliseconds,
        bool useGlobalConsecutiveMessageDelay,
        AiReplyDelayMode consecutiveMessageDelayMode,
        long fixedConsecutiveMessageDelayMilliseconds,
        long minimumConsecutiveMessageDelayMilliseconds,
        long maximumConsecutiveMessageDelayMilliseconds,
        bool useGlobalQuestionPolicy,
        int maximumConsecutiveQuestionTurns,
        bool useGlobalReplyMessageCount,
        int minimumReplyMessageCount,
        int maximumReplyMessageCount,
        out AiAccountAutonomySettings? settings,
        out string errorMessage)
    {
        settings = null;

        if (!Enum.IsDefined(initiativeLevel))
        {
            errorMessage = "主动程度无效。";
            return false;
        }

        if (!AutonomousInteractionSettingsService.TryValidateReplyDelay(
                replyDelayMode,
                fixedReplyDelayMilliseconds,
                minimumReplyDelayMilliseconds,
                maximumReplyDelayMilliseconds,
                out errorMessage))
        {
            return false;
        }

        if (!AutonomousInteractionSettingsService.TryValidateReplyDelay(
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

        if (!AutonomousInteractionSettingsService
                .TryValidateReplyMessageCountRange(
                    minimumReplyMessageCount,
                    maximumReplyMessageCount,
                    out errorMessage))
        {
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.AiAccounts.Any(account => account.Id == aiAccountId))
        {
            errorMessage = "好友不存在。";
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
            canJoinGroupChats,
            useGlobalReplyDelay,
            replyDelayMode,
            fixedReplyDelayMilliseconds,
            minimumReplyDelayMilliseconds,
            maximumReplyDelayMilliseconds,
            useGlobalConsecutiveMessageDelay,
            consecutiveMessageDelayMode,
            fixedConsecutiveMessageDelayMilliseconds,
            minimumConsecutiveMessageDelayMilliseconds,
            maximumConsecutiveMessageDelayMilliseconds,
            useGlobalQuestionPolicy,
            maximumConsecutiveQuestionTurns,
            useGlobalReplyMessageCount,
            minimumReplyMessageCount,
            maximumReplyMessageCount);

        if (dbContext.Entry(storedSettings).State == EntityState.Detached)
        {
            dbContext.AiAccountAutonomySettings.Add(storedSettings);
        }

        dbContext.SaveChanges();
        settings = storedSettings;
        errorMessage = string.Empty;
        return true;
    }
}

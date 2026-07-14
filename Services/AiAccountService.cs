using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.ConsoleApp.Data;
using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Services;

/// <summary>
/// 负责 AI 账号的验证、数据库保存、创建和查找。
/// </summary>
public class AiAccountService
{
    private const int SqliteUniqueConstraintErrorCode = 2067;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    /// <summary>
    /// 创建账号 Service；每个业务操作通过工厂使用一个短生命周期 DbContext。
    /// </summary>
    public AiAccountService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 验证昵称是否可以用于新账号；验证失败时返回可显示的错误信息。
    /// </summary>
    public string? ValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return "昵称不能为空。";
        }

        string trimmedNickname = nickname.Trim();

        if (trimmedNickname.Length > AiAccount.NicknameMaxLength)
        {
            return $"昵称不能超过 {AiAccount.NicknameMaxLength} 个字符。";
        }

        if (FindByNickname(trimmedNickname) is not null)
        {
            return "昵称已存在。";
        }

        return null;
    }

    /// <summary>
    /// 验证并创建 AI 账号；成功时写入数据库，失败时返回明确错误信息。
    /// </summary>
    public bool TryCreateAiAccount(
        string nickname,
        string identityDescription,
        string personality,
        string speakingStyle,
        out AiAccount? aiAccount,
        out string errorMessage)
    {
        aiAccount = null;

        string? validationError = ValidateNickname(nickname);

        if (validationError is not null)
        {
            errorMessage = validationError;
            return false;
        }

        string trimmedIdentityDescription = (identityDescription ?? string.Empty).Trim();
        string trimmedPersonality = (personality ?? string.Empty).Trim();
        string trimmedSpeakingStyle = (speakingStyle ?? string.Empty).Trim();

        string? textLengthError = ValidateOptionalTextLengths(
            trimmedIdentityDescription,
            trimmedPersonality,
            trimmedSpeakingStyle);

        if (textLengthError is not null)
        {
            errorMessage = textLengthError;
            return false;
        }

        AiAccount newAiAccount = new(
            nickname.Trim(),
            trimmedIdentityDescription,
            trimmedPersonality,
            trimmedSpeakingStyle);

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        dbContext.AiAccounts.Add(newAiAccount);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            errorMessage = "昵称已存在。";
            return false;
        }

        aiAccount = newAiAccount;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 按 Id 查找 AI 账号；未找到时返回 null。
    /// </summary>
    public AiAccount? FindById(Guid id)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.AiAccounts
            .AsNoTracking()
            .SingleOrDefault(account => account.Id == id);
    }

    /// <summary>
    /// 按昵称查找 AI 账号；比较时忽略大小写。
    /// </summary>
    public AiAccount? FindByNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return null;
        }

        string trimmedNickname = nickname.Trim();

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.AiAccounts
            .AsNoTracking()
            .SingleOrDefault(account => account.Nickname == trimmedNickname);
    }

    /// <summary>
    /// 按创建时间返回全部 AI 账号的只读列表。
    /// </summary>
    public IReadOnlyList<AiAccount> GetAllAccounts()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        List<AiAccount> aiAccounts = dbContext.AiAccounts
            .AsNoTracking()
            .OrderBy(account => account.CreatedAt)
            .ThenBy(account => account.Id)
            .ToList();

        return aiAccounts.AsReadOnly();
    }

    /// <summary>
    /// 验证允许留空的描述字段仍然符合数据库长度限制。
    /// </summary>
    private static string? ValidateOptionalTextLengths(
        string identityDescription,
        string personality,
        string speakingStyle)
    {
        if (identityDescription.Length > AiAccount.IdentityDescriptionMaxLength)
        {
            return $"身份描述不能超过 {AiAccount.IdentityDescriptionMaxLength} 个字符。";
        }

        if (personality.Length > AiAccount.PersonalityMaxLength)
        {
            return $"性格不能超过 {AiAccount.PersonalityMaxLength} 个字符。";
        }

        if (speakingStyle.Length > AiAccount.SpeakingStyleMaxLength)
        {
            return $"说话风格不能超过 {AiAccount.SpeakingStyleMaxLength} 个字符。";
        }

        return null;
    }

    /// <summary>
    /// 只将 SQLite 唯一索引冲突转换为昵称重复，其余数据库错误继续向上传递。
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode == SqliteUniqueConstraintErrorCode;
    }
}

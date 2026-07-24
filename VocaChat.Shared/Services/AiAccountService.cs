using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责 AI 账号的验证、数据库保存、创建和查找。
/// </summary>
public class AiAccountService
{
    private const int SqliteUniqueConstraintErrorCode = 2067;
    private const int DefaultVcNumberMinimum = 1_000_000;
    private const int DefaultVcNumberMaximumExclusive = 10_000_000;
    private const int DefaultVcNumberGenerationAttempts = 20;
    private const int MaximumTagsPerType = 12;

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
    /// 验证可选的自定义 VC号；留空表示创建时由系统生成默认号码。
    /// </summary>
    public string? ValidateVcNumber(string? vcNumber)
    {
        if (string.IsNullOrWhiteSpace(vcNumber))
        {
            return null;
        }

        string trimmedVcNumber = vcNumber.Trim();

        if (trimmedVcNumber.Length > AiAccount.VcNumberMaxLength)
        {
            return $"VC号不能超过 {AiAccount.VcNumberMaxLength} 个字符。";
        }

        if (FindByVcNumber(trimmedVcNumber) is not null)
        {
            return "VC号已存在。";
        }

        return null;
    }

    /// <summary>
    /// 使用系统生成的默认 VC号创建 AI 账号，保留原有调用方式。
    /// </summary>
    public bool TryCreateAiAccount(
        string nickname,
        string identityDescription,
        string personality,
        string speakingStyle,
        out AiAccount? aiAccount,
        out string errorMessage)
    {
        return TryCreateAiAccount(
            nickname,
            null,
            identityDescription,
            personality,
            speakingStyle,
            out aiAccount,
            out errorMessage);
    }

    /// <summary>
    /// 验证并创建 AI 账号；VC号留空时生成唯一的 7 位随机数字号。
    /// </summary>
    public bool TryCreateAiAccount(
        string nickname,
        string? vcNumber,
        string identityDescription,
        string personality,
        string speakingStyle,
        out AiAccount? aiAccount,
        out string errorMessage)
    {
        return TryCreateAiAccount(
            new AiAccountCreationData
            {
                Nickname = nickname,
                VcNumber = vcNumber,
                IdentityDescription = identityDescription,
                Personality = personality,
                SpeakingStyle = speakingStyle
            },
            out aiAccount,
            out errorMessage);
    }

    /// <summary>
    /// 验证并保存完整好友档案；VC号留空时生成唯一的 7 位随机数字号。
    /// </summary>
    public bool TryCreateAiAccount(
        AiAccountCreationData creationData,
        out AiAccount? aiAccount,
        out string errorMessage)
    {
        aiAccount = null;

        if (creationData is null)
        {
            errorMessage = "账号资料不能为空。";
            return false;
        }

        string? nicknameValidationError = ValidateNickname(creationData.Nickname);

        if (nicknameValidationError is not null)
        {
            errorMessage = nicknameValidationError;
            return false;
        }

        string? vcNumberValidationError = ValidateVcNumber(creationData.VcNumber);

        if (vcNumberValidationError is not null)
        {
            errorMessage = vcNumberValidationError;
            return false;
        }

        string trimmedIdentityDescription = creationData.IdentityDescription.Trim();
        string trimmedPersonality = creationData.Personality.Trim();
        string trimmedSpeakingStyle = creationData.SpeakingStyle.Trim();
        string trimmedSignature = creationData.Signature.Trim();
        string trimmedLocation = creationData.Location.Trim();
        string trimmedOccupation = creationData.Occupation.Trim();
        string trimmedHometown = creationData.Hometown.Trim();

        string? textLengthError = ValidateOptionalTextLengths(
            trimmedIdentityDescription,
            trimmedPersonality,
            trimmedSpeakingStyle,
            trimmedSignature,
            trimmedLocation,
            trimmedOccupation,
            trimmedHometown);

        if (textLengthError is not null)
        {
            errorMessage = textLengthError;
            return false;
        }

        if (creationData.Birthday > DateOnly.FromDateTime(DateTime.Today))
        {
            errorMessage = "生日不能晚于今天。";
            return false;
        }

        if (!Enum.IsDefined(creationData.Gender))
        {
            errorMessage = "性别值无效。";
            return false;
        }

        if (!Enum.IsDefined(creationData.OnlineStatus))
        {
            errorMessage = "在线状态值无效。";
            return false;
        }

        if (!TryNormalizeTags(
                creationData.InterestTags,
                "兴趣标签",
                out IReadOnlyList<string> interestTags,
                out errorMessage)
            || !TryNormalizeTags(
                creationData.PersonalityTags,
                "个性标签",
                out IReadOnlyList<string> personalityTags,
                out errorMessage))
        {
            return false;
        }

        bool shouldGenerateVcNumber = string.IsNullOrWhiteSpace(creationData.VcNumber);
        string? trimmedCustomVcNumber = shouldGenerateVcNumber
            ? null
            : creationData.VcNumber!.Trim();
        int saveAttempts = shouldGenerateVcNumber
            ? DefaultVcNumberGenerationAttempts
            : 1;

        for (int attempt = 0; attempt < saveAttempts; attempt++)
        {
            string selectedVcNumber = trimmedCustomVcNumber
                ?? GenerateDefaultVcNumber();

            if (FindByVcNumber(selectedVcNumber) is not null)
            {
                if (shouldGenerateVcNumber)
                {
                    continue;
                }

                errorMessage = "VC号已存在。";
                return false;
            }

            AiAccount newAiAccount = new(
                selectedVcNumber,
                creationData.Nickname.Trim(),
                trimmedIdentityDescription,
                trimmedPersonality,
                trimmedSpeakingStyle,
                trimmedSignature,
                creationData.Birthday,
                creationData.Gender,
                trimmedLocation,
                trimmedOccupation,
                trimmedHometown,
                creationData.OnlineStatus);

            foreach (string tag in interestTags)
            {
                newAiAccount.AddTag(AiAccountTagType.Interest, tag);
            }

            foreach (string tag in personalityTags)
            {
                newAiAccount.AddTag(AiAccountTagType.Personality, tag);
            }

            using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
            Guid selectedWorldId = creationData.CharacterWorldId
                ?? CharacterWorld.DefaultWorldId;
            CharacterWorld? selectedWorld = dbContext.CharacterWorlds
                .SingleOrDefault(world => world.Id == selectedWorldId);
            if (selectedWorld is null)
            {
                errorMessage = "角色世界不存在。";
                return false;
            }

            newAiAccount.AssignCharacterWorld(selectedWorld);
            dbContext.AiAccounts.Add(newAiAccount);
            dbContext.Contacts.Add(new Contact(
                newAiAccount.Id,
                ContactGroup.DefaultGroupId));

            try
            {
                dbContext.SaveChanges();
                aiAccount = newAiAccount;
                errorMessage = string.Empty;
                return true;
            }
            catch (DbUpdateException exception)
                when (IsUniqueConstraintViolation(exception))
            {
                if (IsVcNumberUniqueConstraintViolation(exception))
                {
                    if (shouldGenerateVcNumber)
                    {
                        continue;
                    }

                    errorMessage = "VC号已存在。";
                    return false;
                }

                errorMessage = "昵称已存在。";
                return false;
            }
        }

        errorMessage = "暂时无法生成可用的 VC号，请重试。";
        return false;
    }

    /// <summary>
    /// 验证并更新完整账号档案；账号关系、创建时间和媒体标识保持不变。
    /// </summary>
    public AiAccountUpdateStatus TryUpdateAiAccount(
        Guid aiAccountId,
        AiAccountUpdateData updateData,
        out AiAccount? aiAccount,
        out string errorMessage)
    {
        aiAccount = null;

        if (updateData is null)
        {
            errorMessage = "账号资料不能为空。";
            return AiAccountUpdateStatus.InvalidData;
        }

        if (string.IsNullOrWhiteSpace(updateData.Nickname))
        {
            errorMessage = "昵称不能为空。";
            return AiAccountUpdateStatus.InvalidData;
        }

        if (string.IsNullOrWhiteSpace(updateData.VcNumber))
        {
            errorMessage = "VC号不能为空。";
            return AiAccountUpdateStatus.InvalidData;
        }

        string trimmedNickname = updateData.Nickname.Trim();
        string trimmedVcNumber = updateData.VcNumber.Trim();
        string trimmedIdentityDescription =
            updateData.IdentityDescription.Trim();
        string trimmedPersonality = updateData.Personality.Trim();
        string trimmedSpeakingStyle = updateData.SpeakingStyle.Trim();
        string trimmedSignature = updateData.Signature.Trim();
        string trimmedLocation = updateData.Location.Trim();
        string trimmedOccupation = updateData.Occupation.Trim();
        string trimmedHometown = updateData.Hometown.Trim();

        if (trimmedNickname.Length > AiAccount.NicknameMaxLength)
        {
            errorMessage =
                $"昵称不能超过 {AiAccount.NicknameMaxLength} 个字符。";
            return AiAccountUpdateStatus.InvalidData;
        }

        if (trimmedVcNumber.Length > AiAccount.VcNumberMaxLength)
        {
            errorMessage =
                $"VC号不能超过 {AiAccount.VcNumberMaxLength} 个字符。";
            return AiAccountUpdateStatus.InvalidData;
        }

        string? textLengthError = ValidateOptionalTextLengths(
            trimmedIdentityDescription,
            trimmedPersonality,
            trimmedSpeakingStyle,
            trimmedSignature,
            trimmedLocation,
            trimmedOccupation,
            trimmedHometown);
        if (textLengthError is not null)
        {
            errorMessage = textLengthError;
            return AiAccountUpdateStatus.InvalidData;
        }

        if (updateData.Birthday > DateOnly.FromDateTime(DateTime.Today))
        {
            errorMessage = "生日不能晚于今天。";
            return AiAccountUpdateStatus.InvalidData;
        }

        if (!Enum.IsDefined(updateData.Gender))
        {
            errorMessage = "性别值无效。";
            return AiAccountUpdateStatus.InvalidData;
        }

        if (!Enum.IsDefined(updateData.OnlineStatus))
        {
            errorMessage = "在线状态值无效。";
            return AiAccountUpdateStatus.InvalidData;
        }

        if (!TryNormalizeTags(
                updateData.InterestTags,
                "兴趣标签",
                out IReadOnlyList<string> interestTags,
                out errorMessage)
            || !TryNormalizeTags(
                updateData.PersonalityTags,
                "个性标签",
                out IReadOnlyList<string> personalityTags,
                out errorMessage))
        {
            return AiAccountUpdateStatus.InvalidData;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiAccount? storedAccount = dbContext.AiAccounts
            .Include(account => account.Tags)
            .Include(account => account.CharacterWorld)
            .SingleOrDefault(account => account.Id == aiAccountId);
        if (storedAccount is null)
        {
            errorMessage = "AI 账号不存在。";
            return AiAccountUpdateStatus.AccountNotFound;
        }

        if (dbContext.AiAccounts.Any(account =>
                account.Id != aiAccountId
                && account.Nickname == trimmedNickname))
        {
            errorMessage = "昵称已存在。";
            return AiAccountUpdateStatus.DuplicateNickname;
        }

        if (dbContext.AiAccounts.Any(account =>
                account.Id != aiAccountId
                && account.VcNumber == trimmedVcNumber))
        {
            errorMessage = "VC号已存在。";
            return AiAccountUpdateStatus.DuplicateVcNumber;
        }

        if (updateData.CharacterWorldId.HasValue)
        {
            CharacterWorld? selectedWorld = dbContext.CharacterWorlds
                .SingleOrDefault(world =>
                    world.Id == updateData.CharacterWorldId.Value);
            if (selectedWorld is null)
            {
                errorMessage = "角色世界不存在。";
                return AiAccountUpdateStatus.CharacterWorldNotFound;
            }

            storedAccount.AssignCharacterWorld(selectedWorld);
        }

        storedAccount.UpdateProfile(
            trimmedVcNumber,
            trimmedNickname,
            trimmedIdentityDescription,
            trimmedPersonality,
            trimmedSpeakingStyle,
            trimmedSignature,
            updateData.Birthday,
            updateData.Gender,
            trimmedLocation,
            trimmedOccupation,
            trimmedHometown,
            updateData.OnlineStatus);
        storedAccount.SynchronizeTags(
            AiAccountTagType.Interest,
            interestTags);
        storedAccount.SynchronizeTags(
            AiAccountTagType.Personality,
            personalityTags);

        try
        {
            dbContext.SaveChanges();
            aiAccount = storedAccount;
            errorMessage = string.Empty;
            return AiAccountUpdateStatus.Success;
        }
        catch (DbUpdateException exception)
            when (IsVcNumberUniqueConstraintViolation(exception))
        {
            errorMessage = "VC号已存在。";
            return AiAccountUpdateStatus.DuplicateVcNumber;
        }
        catch (DbUpdateException exception)
            when (IsNicknameUniqueConstraintViolation(exception))
        {
            errorMessage = "昵称已存在。";
            return AiAccountUpdateStatus.DuplicateNickname;
        }
        catch (DbUpdateException)
        {
            errorMessage = "账号资料暂时无法保存，请稍后重试。";
            return AiAccountUpdateStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "账号资料暂时无法保存，请稍后重试。";
            return AiAccountUpdateStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 修改已有账号的 VC号；内部 Guid 主键和已有业务关系保持不变。
    /// </summary>
    public bool TryChangeVcNumber(
        Guid aiAccountId,
        string vcNumber,
        out AiAccount? aiAccount,
        out string errorMessage)
    {
        aiAccount = null;

        if (string.IsNullOrWhiteSpace(vcNumber))
        {
            errorMessage = "VC号不能为空。";
            return false;
        }

        string trimmedVcNumber = vcNumber.Trim();

        if (trimmedVcNumber.Length > AiAccount.VcNumberMaxLength)
        {
            errorMessage = $"VC号不能超过 {AiAccount.VcNumberMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiAccount? storedAccount = dbContext.AiAccounts
            .Include(account => account.CharacterWorld)
            .SingleOrDefault(account => account.Id == aiAccountId);

        if (storedAccount is null)
        {
            errorMessage = "AI 账号不存在。";
            return false;
        }

        bool vcNumberExists = dbContext.AiAccounts.Any(account =>
            account.Id != aiAccountId
            && account.VcNumber == trimmedVcNumber);

        if (vcNumberExists)
        {
            errorMessage = "VC号已存在。";
            return false;
        }

        storedAccount.ChangeVcNumber(trimmedVcNumber);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
            when (IsVcNumberUniqueConstraintViolation(exception))
        {
            errorMessage = "VC号已存在。";
            return false;
        }

        aiAccount = storedAccount;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 将已有账号切换到新的头像媒体标识，并返回替换前的标识供文件清理使用。
    /// </summary>
    public bool TryChangeAvatarMediaId(
        Guid aiAccountId,
        string mediaId,
        out AiAccount? aiAccount,
        out string? previousMediaId,
        out string errorMessage)
    {
        return TryChangeMediaId(
            aiAccountId,
            mediaId,
            AiAccountMediaKind.Avatar,
            out aiAccount,
            out previousMediaId,
            out errorMessage);
    }

    /// <summary>
    /// 将已有账号切换到新的主页封面媒体标识，并返回替换前的标识供文件清理使用。
    /// </summary>
    public bool TryChangeProfileCoverMediaId(
        Guid aiAccountId,
        string mediaId,
        out AiAccount? aiAccount,
        out string? previousMediaId,
        out string errorMessage)
    {
        return TryChangeMediaId(
            aiAccountId,
            mediaId,
            AiAccountMediaKind.ProfileCover,
            out aiAccount,
            out previousMediaId,
            out errorMessage);
    }

    /// <summary>
    /// 按 Id 查找 AI 账号；未找到时返回 null。
    /// </summary>
    public AiAccount? FindById(Guid id)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.AiAccounts
            .Include(account => account.Tags)
            .Include(account => account.CharacterWorld)
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
            .Include(account => account.Tags)
            .Include(account => account.CharacterWorld)
            .AsNoTracking()
            .SingleOrDefault(account => account.Nickname == trimmedNickname);
    }

    /// <summary>
    /// 按面向用户展示的 VC号查找账号；英文比较忽略大小写。
    /// </summary>
    public AiAccount? FindByVcNumber(string vcNumber)
    {
        if (string.IsNullOrWhiteSpace(vcNumber))
        {
            return null;
        }

        string trimmedVcNumber = vcNumber.Trim();

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.AiAccounts
            .Include(account => account.Tags)
            .Include(account => account.CharacterWorld)
            .AsNoTracking()
            .SingleOrDefault(account => account.VcNumber == trimmedVcNumber);
    }

    /// <summary>
    /// 按创建时间返回全部 AI 账号的只读列表。
    /// </summary>
    public IReadOnlyList<AiAccount> GetAllAccounts()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        List<AiAccount> aiAccounts = dbContext.AiAccounts
            .Include(account => account.Tags)
            .Include(account => account.CharacterWorld)
            .AsNoTracking()
            .OrderBy(account => account.CreatedAt)
            .ThenBy(account => account.Id)
            .ToList();

        return aiAccounts.AsReadOnly();
    }

    private static string GenerateDefaultVcNumber()
    {
        int randomNumber = RandomNumberGenerator.GetInt32(
            DefaultVcNumberMinimum,
            DefaultVcNumberMaximumExclusive);

        return randomNumber.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 在一个短生命周期 DbContext 中更新媒体标识，数据库仍然只保存不透明标识。
    /// </summary>
    private bool TryChangeMediaId(
        Guid aiAccountId,
        string mediaId,
        AiAccountMediaKind mediaKind,
        out AiAccount? aiAccount,
        out string? previousMediaId,
        out string errorMessage)
    {
        aiAccount = null;
        previousMediaId = null;

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            errorMessage = "媒体标识不能为空。";
            return false;
        }

        string trimmedMediaId = mediaId.Trim();

        if (trimmedMediaId.Length > AiAccount.MediaIdMaxLength)
        {
            errorMessage = $"媒体标识不能超过 {AiAccount.MediaIdMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiAccount? storedAccount = dbContext.AiAccounts
            .Include(account => account.Tags)
            .Include(account => account.CharacterWorld)
            .SingleOrDefault(account => account.Id == aiAccountId);

        if (storedAccount is null)
        {
            errorMessage = "AI 账号不存在。";
            return false;
        }

        if (mediaKind == AiAccountMediaKind.Avatar)
        {
            previousMediaId = storedAccount.AvatarMediaId;
            storedAccount.ChangeAvatarMediaId(trimmedMediaId);
        }
        else
        {
            previousMediaId = storedAccount.ProfileCoverMediaId;
            storedAccount.ChangeProfileCoverMediaId(trimmedMediaId);
        }

        dbContext.SaveChanges();
        aiAccount = storedAccount;
        errorMessage = string.Empty;
        return true;
    }

    private enum AiAccountMediaKind
    {
        Avatar,
        ProfileCover
    }

    /// <summary>
    /// 验证允许留空的描述字段仍然符合数据库长度限制。
    /// </summary>
    private static string? ValidateOptionalTextLengths(
        string identityDescription,
        string personality,
        string speakingStyle,
        string signature,
        string location,
        string occupation,
        string hometown)
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

        if (signature.Length > AiAccount.SignatureMaxLength)
        {
            return $"个性签名不能超过 {AiAccount.SignatureMaxLength} 个字符。";
        }

        if (location.Length > AiAccount.LocationMaxLength)
        {
            return $"所在地不能超过 {AiAccount.LocationMaxLength} 个字符。";
        }

        if (occupation.Length > AiAccount.OccupationMaxLength)
        {
            return $"职业不能超过 {AiAccount.OccupationMaxLength} 个字符。";
        }

        if (hometown.Length > AiAccount.HometownMaxLength)
        {
            return $"故乡不能超过 {AiAccount.HometownMaxLength} 个字符。";
        }

        return null;
    }

    /// <summary>
    /// 清理标签空白、忽略大小写去重，并保护单个标签和标签数量限制。
    /// </summary>
    private static bool TryNormalizeTags(
        IReadOnlyCollection<string>? source,
        string displayName,
        out IReadOnlyList<string> normalizedTags,
        out string errorMessage)
    {
        List<string> tags = (source ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tags.Count > MaximumTagsPerType)
        {
            normalizedTags = Array.Empty<string>();
            errorMessage = $"{displayName}最多添加 {MaximumTagsPerType} 个。";
            return false;
        }

        if (tags.Any(tag => tag.Length > AiAccountTag.ValueMaxLength))
        {
            normalizedTags = Array.Empty<string>();
            errorMessage = $"单个{displayName}不能超过 {AiAccountTag.ValueMaxLength} 个字符。";
            return false;
        }

        normalizedTags = tags.AsReadOnly();
        errorMessage = string.Empty;
        return true;
    }

    private static bool IsVcNumberUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return IsUniqueConstraintViolation(exception)
            && exception.InnerException?.Message.Contains(
                "AiAccounts.VcNumber",
                StringComparison.Ordinal) == true;
    }

    private static bool IsNicknameUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return IsUniqueConstraintViolation(exception)
            && exception.InnerException?.Message.Contains(
                "AiAccounts.Nickname",
                StringComparison.Ordinal) == true;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode == SqliteUniqueConstraintErrorCode;
    }
}

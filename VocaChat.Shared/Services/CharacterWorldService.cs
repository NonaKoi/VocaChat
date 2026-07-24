using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责角色世界的验证、创建、查询和更新。
/// </summary>
public sealed class CharacterWorldService
{
    private const int SqliteUniqueConstraintErrorCode = 2067;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    public CharacterWorldService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 返回全部角色世界，供账号资料选择和世界维护使用。
    /// </summary>
    public IReadOnlyList<CharacterWorld> GetAll()
    {
        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();

        return dbContext.CharacterWorlds
            .AsNoTracking()
            .OrderBy(world => world.CreatedAt)
            .ThenBy(world => world.Id)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 按 Id 查找角色世界；不存在时返回 null。
    /// </summary>
    public CharacterWorld? FindById(Guid id)
    {
        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();

        return dbContext.CharacterWorlds
            .AsNoTracking()
            .SingleOrDefault(world => world.Id == id);
    }

    /// <summary>
    /// 验证并创建角色世界；世界名称忽略大小写且不能重复。
    /// </summary>
    public CharacterWorldOperationStatus TryCreate(
        string name,
        string description,
        out CharacterWorld? characterWorld,
        out string errorMessage)
    {
        characterWorld = null;

        if (!TryNormalize(
                name,
                description,
                out string normalizedName,
                out string normalizedDescription,
                out errorMessage))
        {
            return CharacterWorldOperationStatus.InvalidData;
        }

        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();

        if (dbContext.CharacterWorlds.Any(
                world => world.Name == normalizedName))
        {
            errorMessage = "角色世界名称已存在。";
            return CharacterWorldOperationStatus.DuplicateName;
        }

        CharacterWorld newWorld = new(
            normalizedName,
            normalizedDescription);
        dbContext.CharacterWorlds.Add(newWorld);

        try
        {
            dbContext.SaveChanges();
            characterWorld = newWorld;
            errorMessage = string.Empty;
            return CharacterWorldOperationStatus.Success;
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            errorMessage = "角色世界名称已存在。";
            return CharacterWorldOperationStatus.DuplicateName;
        }
        catch (DbUpdateException)
        {
            errorMessage = "角色世界暂时无法保存，请稍后重试。";
            return CharacterWorldOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 更新已有世界的名称和说明；引用它的账号继续共享同一实体。
    /// </summary>
    public CharacterWorldOperationStatus TryUpdate(
        Guid id,
        string name,
        string description,
        out CharacterWorld? characterWorld,
        out string errorMessage)
    {
        characterWorld = null;

        if (!TryNormalize(
                name,
                description,
                out string normalizedName,
                out string normalizedDescription,
                out errorMessage))
        {
            return CharacterWorldOperationStatus.InvalidData;
        }

        using VocaChatDbContext dbContext =
            _dbContextFactory.CreateDbContext();
        CharacterWorld? storedWorld = dbContext.CharacterWorlds
            .SingleOrDefault(world => world.Id == id);

        if (storedWorld is null)
        {
            errorMessage = "角色世界不存在。";
            return CharacterWorldOperationStatus.NotFound;
        }

        if (dbContext.CharacterWorlds.Any(world =>
                world.Id != id
                && world.Name == normalizedName))
        {
            errorMessage = "角色世界名称已存在。";
            return CharacterWorldOperationStatus.DuplicateName;
        }

        storedWorld.Update(normalizedName, normalizedDescription);

        try
        {
            dbContext.SaveChanges();
            characterWorld = storedWorld;
            errorMessage = string.Empty;
            return CharacterWorldOperationStatus.Success;
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            errorMessage = "角色世界名称已存在。";
            return CharacterWorldOperationStatus.DuplicateName;
        }
        catch (DbUpdateException)
        {
            errorMessage = "角色世界暂时无法保存，请稍后重试。";
            return CharacterWorldOperationStatus.PersistenceFailed;
        }
    }

    private static bool TryNormalize(
        string name,
        string description,
        out string normalizedName,
        out string normalizedDescription,
        out string errorMessage)
    {
        normalizedName = name?.Trim() ?? string.Empty;
        normalizedDescription = description?.Trim() ?? string.Empty;

        if (normalizedName.Length == 0)
        {
            errorMessage = "角色世界名称不能为空。";
            return false;
        }

        if (normalizedName.Length > CharacterWorld.NameMaxLength)
        {
            errorMessage =
                $"角色世界名称不能超过 {CharacterWorld.NameMaxLength} 个字符。";
            return false;
        }

        if (normalizedDescription.Length
            > CharacterWorld.DescriptionMaxLength)
        {
            errorMessage =
                $"角色世界说明不能超过 {CharacterWorld.DescriptionMaxLength} 个字符。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                == SqliteUniqueConstraintErrorCode;
    }
}

/// <summary>
/// 表示角色世界创建或更新的明确业务结果。
/// </summary>
public enum CharacterWorldOperationStatus
{
    Success,
    NotFound,
    InvalidData,
    DuplicateName,
    PersistenceFailed
}

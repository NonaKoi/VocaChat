using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责有方向长期记忆的来源验证、持久化、幂等保护和候选查询。
/// </summary>
public sealed class AiMemoryService
{
    private const int MaximumQueryCount = 20;
    private const int SqliteUniqueConstraintErrorCode = 2067;
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiMemoryService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 保存一条来自已有自主私信 Session 的方向记忆。
    /// 相同来源、方向、类型和摘要的重试会返回已有记录。
    /// </summary>
    public AiMemoryOperationStatus TryCreateMemory(
        Guid ownerAiAccountId,
        Guid subjectAiAccountId,
        AiMemoryType type,
        string summary,
        int salience,
        Guid sourcePrivateChatId,
        Guid sourceSessionId,
        DateTime occurredAt,
        out AiMemory? memory,
        out string errorMessage)
    {
        memory = null;

        AiMemoryOperationStatus inputStatus = ValidateInput(
            ownerAiAccountId,
            subjectAiAccountId,
            type,
            summary,
            salience,
            out string normalizedSummary,
            out errorMessage);

        if (inputStatus != AiMemoryOperationStatus.Success)
        {
            return inputStatus;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiMemoryOperationStatus sourceStatus = ValidateSource(
            dbContext,
            ownerAiAccountId,
            subjectAiAccountId,
            sourcePrivateChatId,
            sourceSessionId,
            out errorMessage);

        if (sourceStatus != AiMemoryOperationStatus.Success)
        {
            return sourceStatus;
        }

        memory = FindExisting(
            dbContext,
            sourceSessionId,
            ownerAiAccountId,
            subjectAiAccountId,
            type,
            normalizedSummary);

        if (memory is not null)
        {
            errorMessage = string.Empty;
            return AiMemoryOperationStatus.AlreadyExists;
        }

        AiMemory newMemory = new(
            ownerAiAccountId,
            subjectAiAccountId,
            type,
            normalizedSummary,
            salience,
            sourcePrivateChatId,
            sourceSessionId,
            occurredAt,
            DateTime.Now);
        dbContext.AiMemories.Add(newMemory);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            using VocaChatDbContext retryContext =
                _dbContextFactory.CreateDbContext();
            memory = FindExisting(
                retryContext,
                sourceSessionId,
                ownerAiAccountId,
                subjectAiAccountId,
                type,
                normalizedSummary);

            if (memory is null)
            {
                errorMessage = "方向记忆发生数据库冲突，但无法读取已有记录。";
                return AiMemoryOperationStatus.PersistenceFailed;
            }

            errorMessage = string.Empty;
            return AiMemoryOperationStatus.AlreadyExists;
        }
        catch (DbUpdateException)
        {
            errorMessage = "方向记忆暂时无法保存，请稍后重试。";
            return AiMemoryOperationStatus.PersistenceFailed;
        }

        memory = newMemory;
        errorMessage = string.Empty;
        return AiMemoryOperationStatus.Success;
    }

    /// <summary>
    /// 按所有者和对象查询少量有效记忆，不在普通读取时修改召回时间。
    /// </summary>
    public AiMemoryOperationStatus TryGetActiveMemories(
        Guid ownerAiAccountId,
        Guid subjectAiAccountId,
        int maximumCount,
        AiMemoryType? type,
        out IReadOnlyList<AiMemory> memories,
        out string errorMessage)
    {
        memories = Array.Empty<AiMemory>();

        if (ownerAiAccountId == subjectAiAccountId)
        {
            errorMessage = "方向记忆的所有者和对象不能是同一个账号。";
            return AiMemoryOperationStatus.SelfMemoryNotAllowed;
        }

        if (maximumCount is < 1 or > MaximumQueryCount)
        {
            errorMessage = $"单次最多只能查询 1 到 {MaximumQueryCount} 条记忆。";
            return AiMemoryOperationStatus.InvalidLimit;
        }

        if (type is not null && !Enum.IsDefined(type.Value))
        {
            errorMessage = "记忆类型无效。";
            return AiMemoryOperationStatus.InvalidType;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!AccountsExist(
                    dbContext,
                    ownerAiAccountId,
                    subjectAiAccountId))
            {
                errorMessage = "记忆所有者或对象账号不存在。";
                return AiMemoryOperationStatus.AccountNotFound;
            }

            IQueryable<AiMemory> query = dbContext.AiMemories
                .AsNoTracking()
                .Where(memory =>
                    memory.OwnerAiAccountId == ownerAiAccountId
                    && memory.SubjectAiAccountId == subjectAiAccountId
                    && memory.IsActive);

            if (type is not null)
            {
                query = query.Where(memory => memory.Type == type.Value);
            }

            memories = query
                .OrderByDescending(memory => memory.Salience)
                .ThenByDescending(memory => memory.OccurredAt)
                .ThenBy(memory => memory.Id)
                .Take(maximumCount)
                .ToList()
                .AsReadOnly();
            errorMessage = string.Empty;
            return AiMemoryOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "方向记忆暂时无法读取，本次对话将不使用长期记忆。";
            return AiMemoryOperationStatus.PersistenceFailed;
        }
    }

    private static AiMemoryOperationStatus ValidateInput(
        Guid ownerAiAccountId,
        Guid subjectAiAccountId,
        AiMemoryType type,
        string summary,
        int salience,
        out string normalizedSummary,
        out string errorMessage)
    {
        normalizedSummary = string.Empty;

        if (ownerAiAccountId == subjectAiAccountId)
        {
            errorMessage = "方向记忆的所有者和对象不能是同一个账号。";
            return AiMemoryOperationStatus.SelfMemoryNotAllowed;
        }

        if (!Enum.IsDefined(type))
        {
            errorMessage = "记忆类型无效。";
            return AiMemoryOperationStatus.InvalidType;
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            errorMessage = "记忆摘要不能为空。";
            return AiMemoryOperationStatus.InvalidSummary;
        }

        normalizedSummary = summary.Trim();

        if (normalizedSummary.Length > AiMemory.SummaryMaxLength)
        {
            errorMessage = $"记忆摘要不能超过 {AiMemory.SummaryMaxLength} 个字符。";
            return AiMemoryOperationStatus.InvalidSummary;
        }

        if (salience is < AiMemory.MinimumSalience
            or > AiMemory.MaximumSalience)
        {
            errorMessage = "记忆重要度必须在 1 到 100 之间。";
            return AiMemoryOperationStatus.InvalidSalience;
        }

        errorMessage = string.Empty;
        return AiMemoryOperationStatus.Success;
    }

    private static AiMemoryOperationStatus ValidateSource(
        VocaChatDbContext dbContext,
        Guid ownerAiAccountId,
        Guid subjectAiAccountId,
        Guid sourcePrivateChatId,
        Guid sourceSessionId,
        out string errorMessage)
    {
        if (!AccountsExist(
                dbContext,
                ownerAiAccountId,
                subjectAiAccountId))
        {
            errorMessage = "记忆所有者或对象账号不存在。";
            return AiMemoryOperationStatus.AccountNotFound;
        }

        PrivateChat? privateChat = dbContext.PrivateChats
            .AsNoTracking()
            .SingleOrDefault(chat => chat.Id == sourcePrivateChatId);
        AutonomousPrivateChatSession? session =
            dbContext.AutonomousPrivateChatSessions
                .AsNoTracking()
                .SingleOrDefault(item => item.Id == sourceSessionId);

        if (privateChat is null || session is null)
        {
            errorMessage = "记忆来源私信或 Session 不存在。";
            return AiMemoryOperationStatus.SourceNotFound;
        }

        if (!ChatParticipantsMatch(
                privateChat,
                ownerAiAccountId,
                subjectAiAccountId)
            || session.PrivateChatId != privateChat.Id
            || !SessionParticipantsMatch(
                session,
                ownerAiAccountId,
                subjectAiAccountId))
        {
            errorMessage = "记忆方向与来源私信或 Session 的参与者不一致。";
            return AiMemoryOperationStatus.SourceMismatch;
        }

        if (session.Status == AutonomousPrivateChatSessionStatus.Running
            || session.CompletedRounds == 0)
        {
            errorMessage = "只有已经结束且完成过普通轮的 Session 才能形成长期记忆。";
            return AiMemoryOperationStatus.SessionNotEligible;
        }

        errorMessage = string.Empty;
        return AiMemoryOperationStatus.Success;
    }

    private static bool AccountsExist(
        VocaChatDbContext dbContext,
        Guid firstAiAccountId,
        Guid secondAiAccountId)
    {
        return dbContext.AiAccounts.Count(account =>
            account.Id == firstAiAccountId
            || account.Id == secondAiAccountId) == 2;
    }

    private static bool ChatParticipantsMatch(
        PrivateChat privateChat,
        Guid firstAiAccountId,
        Guid secondAiAccountId)
    {
        return privateChat.Kind == PrivateChatKind.AiAccounts
            && ((privateChat.FirstAiAccountId == firstAiAccountId
                    && privateChat.SecondAiAccountId == secondAiAccountId)
                || (privateChat.FirstAiAccountId == secondAiAccountId
                    && privateChat.SecondAiAccountId == firstAiAccountId));
    }

    private static bool SessionParticipantsMatch(
        AutonomousPrivateChatSession session,
        Guid firstAiAccountId,
        Guid secondAiAccountId)
    {
        return (session.InitiatorAiAccountId == firstAiAccountId
                && session.RecipientAiAccountId == secondAiAccountId)
            || (session.InitiatorAiAccountId == secondAiAccountId
                && session.RecipientAiAccountId == firstAiAccountId);
    }

    private static AiMemory? FindExisting(
        VocaChatDbContext dbContext,
        Guid sourceSessionId,
        Guid ownerAiAccountId,
        Guid subjectAiAccountId,
        AiMemoryType type,
        string summary)
    {
        return dbContext.AiMemories
            .AsNoTracking()
            .SingleOrDefault(memory =>
                memory.SourceSessionId == sourceSessionId
                && memory.OwnerAiAccountId == ownerAiAccountId
                && memory.SubjectAiAccountId == subjectAiAccountId
                && memory.Type == type
                && memory.Summary == summary);
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                == SqliteUniqueConstraintErrorCode;
    }
}

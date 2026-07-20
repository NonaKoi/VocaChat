using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 以有限容量保存 AI 互动诊断信息，不记录提示词、模型原始响应或密钥。
/// </summary>
public sealed class AiInteractionDiagnosticLogService
{
    public const int MaximumRetainedEntries = 500;
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiInteractionDiagnosticLogService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public bool TryRecord(
        AiInteractionDiagnosticSeverity severity,
        AiInteractionDiagnosticCode code,
        AiMessageGenerationScenario scenario,
        Guid? aiAccountId,
        Guid? conversationId,
        string summary,
        string detail,
        bool wasRecovered = false)
    {
        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiInteractionDiagnosticLog log = new(
                severity,
                code,
                scenario.ToString(),
                aiAccountId,
                conversationId,
                Truncate(summary, AiInteractionDiagnosticLog.SummaryMaxLength),
                Truncate(detail, AiInteractionDiagnosticLog.DetailMaxLength),
                wasRecovered);
            dbContext.AiInteractionDiagnosticLogs.Add(log);
            dbContext.SaveChanges();

            List<AiInteractionDiagnosticLog> expired = dbContext
                .AiInteractionDiagnosticLogs
                .OrderByDescending(item => item.OccurredAt)
                .ThenByDescending(item => item.Id)
                .Skip(MaximumRetainedEntries)
                .ToList();
            if (expired.Count > 0)
            {
                dbContext.AiInteractionDiagnosticLogs.RemoveRange(expired);
                dbContext.SaveChanges();
            }

            return true;
        }
        catch (Exception)
        {
            // 诊断记录失败不能覆盖原始聊天结果。
            return false;
        }
    }

    public IReadOnlyList<AiInteractionDiagnosticLog> GetRecent(int limit = 100)
    {
        int safeLimit = Math.Clamp(limit, 1, MaximumRetainedEntries);
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.AiInteractionDiagnosticLogs
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Take(safeLimit)
            .ToList()
            .AsReadOnly();
    }

    private static string Truncate(string? value, int maximumLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? "未提供详细信息。"
            : value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}


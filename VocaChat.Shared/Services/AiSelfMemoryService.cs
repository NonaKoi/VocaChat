using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责 AI 个人记忆的验证、持久化、账号隔离和有限上下文查询。
/// </summary>
public sealed class AiSelfMemoryService
{
    private const int MaximumQueryCount = 100;
    private const int MaximumContextMemoryCount = 20;
    private const int MaximumDirectorProposalCount = 2;
    private const int SqliteUniqueConstraintErrorCode = 2067;
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiSelfMemoryService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 创建一条由本地用户确认的个人记忆。
    /// </summary>
    public AiSelfMemoryOperationStatus TryCreateUserMemory(
        Guid aiAccountId,
        AiSelfMemoryType type,
        string summary,
        int salience,
        bool isUserLocked,
        DateTime? occurredAt,
        DateTime? validFrom,
        DateTime? validUntil,
        out AiSelfMemory? memory,
        out string errorMessage)
    {
        memory = null;
        AiSelfMemoryOperationStatus validationStatus = ValidateEditableValues(
            type,
            summary,
            salience,
            validFrom,
            validUntil,
            out string normalizedSummary,
            out errorMessage);

        if (validationStatus != AiSelfMemoryOperationStatus.Success)
        {
            return validationStatus;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!AccountExists(dbContext, aiAccountId))
            {
                errorMessage = "AI 账号不存在。";
                return AiSelfMemoryOperationStatus.AccountNotFound;
            }

            memory = FindActiveDuplicate(
                dbContext,
                aiAccountId,
                type,
                normalizedSummary);
            if (memory is not null)
            {
                errorMessage = "该账号已经存在相同的有效个人记忆。";
                return AiSelfMemoryOperationStatus.AlreadyExists;
            }

            DateTime now = DateTime.Now;
            AiSelfMemory newMemory = new(
                aiAccountId,
                type,
                normalizedSummary,
                AiSelfMemorySource.User,
                salience,
                isUserLocked,
                sourceConversationId: null,
                sourceMessageId: null,
                occurredAt,
                validFrom,
                validUntil,
                now);
            dbContext.AiSelfMemories.Add(newMemory);
            dbContext.SaveChanges();

            memory = newMemory;
            errorMessage = string.Empty;
            return AiSelfMemoryOperationStatus.Success;
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            memory = TryReadActiveDuplicate(
                aiAccountId,
                type,
                normalizedSummary);
            errorMessage = "该账号已经存在相同的有效个人记忆。";
            return memory is null
                ? AiSelfMemoryOperationStatus.PersistenceFailed
                : AiSelfMemoryOperationStatus.AlreadyExists;
        }
        catch (DbUpdateException)
        {
            errorMessage = "个人记忆暂时无法保存，请稍后重试。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "个人记忆暂时无法保存，请稍后重试。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 返回一个账号的个人记忆，可按状态筛选。
    /// </summary>
    public AiSelfMemoryOperationStatus TryGetMemories(
        Guid aiAccountId,
        int maximumCount,
        AiSelfMemoryStatus? status,
        out IReadOnlyList<AiSelfMemory> memories,
        out string errorMessage)
    {
        memories = Array.Empty<AiSelfMemory>();

        if (maximumCount is < 1 or > MaximumQueryCount)
        {
            errorMessage = $"单次最多只能查询 1 到 {MaximumQueryCount} 条个人记忆。";
            return AiSelfMemoryOperationStatus.InvalidLimit;
        }

        if (status is not null && !Enum.IsDefined(status.Value))
        {
            errorMessage = "个人记忆状态无效。";
            return AiSelfMemoryOperationStatus.InvalidStatus;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!AccountExists(dbContext, aiAccountId))
            {
                errorMessage = "AI 账号不存在。";
                return AiSelfMemoryOperationStatus.AccountNotFound;
            }

            IQueryable<AiSelfMemory> query = dbContext.AiSelfMemories
                .AsNoTracking()
                .Where(memory => memory.AiAccountId == aiAccountId);

            if (status is not null)
            {
                query = query.Where(memory => memory.Status == status.Value);
            }

            memories = query
                .OrderBy(memory => memory.Status)
                .ThenByDescending(memory => memory.Salience)
                .ThenByDescending(memory => memory.UpdatedAt)
                .ThenBy(memory => memory.Id)
                .Take(maximumCount)
                .ToList()
                .AsReadOnly();
            errorMessage = string.Empty;
            return AiSelfMemoryOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "个人记忆暂时无法读取，请稍后重试。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 为生成上下文返回少量有效个人记忆，由身份连续性流程进一步筛选。
    /// </summary>
    public AiSelfMemoryOperationStatus TryGetActiveContextMemories(
        Guid aiAccountId,
        int maximumCount,
        out IReadOnlyList<AiSelfMemory> memories,
        out string errorMessage)
    {
        if (maximumCount is < 1 or > MaximumContextMemoryCount)
        {
            memories = Array.Empty<AiSelfMemory>();
            errorMessage = $"生成上下文最多只能读取 1 到 {MaximumContextMemoryCount} 条个人记忆。";
            return AiSelfMemoryOperationStatus.InvalidLimit;
        }

        return TryGetMemories(
            aiAccountId,
            maximumCount,
            AiSelfMemoryStatus.Active,
            out memories,
            out errorMessage);
    }

    /// <summary>
    /// 修改一条属于指定账号的个人记忆；来源和来源消息保持不变。
    /// </summary>
    public AiSelfMemoryOperationStatus TryUpdateUserMemory(
        Guid aiAccountId,
        Guid memoryId,
        AiSelfMemoryType type,
        string summary,
        int salience,
        bool isUserLocked,
        DateTime? occurredAt,
        DateTime? validFrom,
        DateTime? validUntil,
        out AiSelfMemory? memory,
        out string errorMessage)
    {
        memory = null;
        AiSelfMemoryOperationStatus validationStatus = ValidateEditableValues(
            type,
            summary,
            salience,
            validFrom,
            validUntil,
            out string normalizedSummary,
            out errorMessage);

        if (validationStatus != AiSelfMemoryOperationStatus.Success)
        {
            return validationStatus;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiSelfMemoryOperationStatus lookupStatus = FindTrackedMemory(
                dbContext,
                aiAccountId,
                memoryId,
                out AiSelfMemory? storedMemory,
                out errorMessage);

            if (lookupStatus != AiSelfMemoryOperationStatus.Success)
            {
                return lookupStatus;
            }

            storedMemory!.UpdateByUser(
                type,
                normalizedSummary,
                salience,
                isUserLocked,
                occurredAt,
                validFrom,
                validUntil,
                DateTime.Now);
            dbContext.SaveChanges();

            memory = storedMemory;
            errorMessage = string.Empty;
            return AiSelfMemoryOperationStatus.Success;
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            errorMessage = "该账号已经存在相同的有效个人记忆。";
            return AiSelfMemoryOperationStatus.AlreadyExists;
        }
        catch (DbUpdateException)
        {
            errorMessage = "个人记忆暂时无法更新，请稍后重试。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "个人记忆暂时无法更新，请稍后重试。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 允许用户归档或恢复个人记忆；Superseded 状态由后续导演替代流程管理。
    /// </summary>
    public AiSelfMemoryOperationStatus TryChangeUserManagedStatus(
        Guid aiAccountId,
        Guid memoryId,
        AiSelfMemoryStatus status,
        out AiSelfMemory? memory,
        out string errorMessage)
    {
        memory = null;

        if (!Enum.IsDefined(status)
            || status == AiSelfMemoryStatus.Superseded)
        {
            errorMessage = "用户只能将个人记忆设为有效或已归档。";
            return AiSelfMemoryOperationStatus.InvalidStatus;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiSelfMemoryOperationStatus lookupStatus = FindTrackedMemory(
                dbContext,
                aiAccountId,
                memoryId,
                out AiSelfMemory? storedMemory,
                out errorMessage);

            if (lookupStatus != AiSelfMemoryOperationStatus.Success)
            {
                return lookupStatus;
            }

            if (storedMemory!.Status != status)
            {
                storedMemory.ChangeUserManagedStatus(status, DateTime.Now);
                dbContext.SaveChanges();
            }

            memory = storedMemory;
            errorMessage = string.Empty;
            return AiSelfMemoryOperationStatus.Success;
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            errorMessage = "恢复后会与另一条有效个人记忆重复。";
            return AiSelfMemoryOperationStatus.AlreadyExists;
        }
        catch (DbUpdateException)
        {
            errorMessage = "个人记忆状态暂时无法更新，请稍后重试。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "个人记忆状态暂时无法更新，请稍后重试。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 在生成可见消息前预验证导演建议；拒绝单项建议不会阻断本轮聊天。
    /// </summary>
    public AiSelfMemoryOperationStatus TryValidateDirectorProposals(
        Guid aiAccountId,
        IReadOnlyList<AiSelfMemoryProposal> proposals,
        out AiSelfMemoryProposalValidationResult result,
        out string errorMessage)
    {
        result = AiSelfMemoryProposalValidationResult.Empty;

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            if (!AccountExists(dbContext, aiAccountId))
            {
                errorMessage = "AI 账号不存在。";
                return AiSelfMemoryOperationStatus.AccountNotFound;
            }

            List<AiSelfMemoryProposal> accepted = new();
            List<AiSelfMemoryProposalDecision> decisions = new();
            HashSet<Guid> claimedTargets = new();

            for (int index = 0; index < proposals.Count; index++)
            {
                AiSelfMemoryProposal proposal = proposals[index];
                string? rejectionReason = index >= MaximumDirectorProposalCount
                    ? $"每轮最多接受 {MaximumDirectorProposalCount} 项个人记忆建议。"
                    : ValidateDirectorProposal(
                        dbContext,
                        aiAccountId,
                        proposal,
                        claimedTargets);
                bool isAccepted = rejectionReason is null;
                if (isAccepted)
                {
                    accepted.Add(NormalizeProposal(proposal));
                }

                decisions.Add(new AiSelfMemoryProposalDecision(
                    proposal,
                    isAccepted,
                    rejectionReason ?? "建议通过业务预验证。"));
            }

            result = new AiSelfMemoryProposalValidationResult(
                accepted.AsReadOnly(),
                decisions.AsReadOnly());
            errorMessage = string.Empty;
            return AiSelfMemoryOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "个人记忆建议暂时无法验证。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 在 AI 消息正式保存后应用已通过预验证的导演建议。
    /// </summary>
    public AiSelfMemoryProposalApplicationResult ApplyDirectorProposals(
        Guid aiAccountId,
        Guid sourceConversationId,
        IReadOnlyList<AiSelfMemoryProposal> proposals,
        IReadOnlyList<AiPersistedMessageEvidence> savedMessages)
    {
        if (proposals.Count == 0)
        {
            return AiSelfMemoryProposalApplicationResult.Empty;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            if (!AccountExists(dbContext, aiAccountId))
            {
                return FailedApplication("当前发言账号不存在。", proposals.Count);
            }

            IReadOnlyList<AiPersistedMessageEvidence> verifiedMessages =
                savedMessages
                    .Where(message => IsPersistedSpeakerMessage(
                        dbContext,
                        aiAccountId,
                        sourceConversationId,
                        message.MessageId))
                    .ToList()
                    .AsReadOnly();
            if (verifiedMessages.Count == 0)
            {
                return FailedApplication(
                    "没有找到可验证的正式 AI 消息来源。",
                    proposals.Count);
            }

            int appliedCount = 0;
            int alreadyAppliedCount = 0;
            int rejectedCount = 0;
            DateTime now = DateTime.Now;

            foreach (AiSelfMemoryProposal rawProposal in proposals
                         .Take(MaximumDirectorProposalCount))
            {
                AiSelfMemoryProposal proposal = NormalizeProposal(rawProposal);
                AiPersistedMessageEvidence? sourceMessage = verifiedMessages
                    .FirstOrDefault(message =>
                        AiFactGroundingMatcher.HasGroundingOverlap(
                            proposal.Summary,
                            message.Content));
                if (sourceMessage is null)
                {
                    rejectedCount++;
                    continue;
                }

                AiSelfMemory? existingBySource = dbContext.AiSelfMemories
                    .SingleOrDefault(memory =>
                        memory.AiAccountId == aiAccountId
                        && memory.Source == AiSelfMemorySource.Director
                        && memory.SourceMessageId == sourceMessage.MessageId
                        && memory.Type == proposal.Type
                        && memory.Summary == proposal.Summary);
                if (existingBySource is not null)
                {
                    alreadyAppliedCount++;
                    continue;
                }

                if (proposal.Operation == AiSelfMemoryProposalOperation.Add)
                {
                    if (FindActiveDuplicate(
                            dbContext,
                            aiAccountId,
                            proposal.Type,
                            proposal.Summary) is not null)
                    {
                        alreadyAppliedCount++;
                        continue;
                    }

                    dbContext.AiSelfMemories.Add(CreateDirectorMemory(
                        aiAccountId,
                        sourceConversationId,
                        sourceMessage,
                        proposal,
                        now));
                    appliedCount++;
                    continue;
                }

                AiSelfMemory? target = proposal.TargetMemoryId is Guid targetId
                    ? dbContext.AiSelfMemories.SingleOrDefault(memory =>
                        memory.Id == targetId
                        && memory.AiAccountId == aiAccountId)
                    : null;
                if (target is null
                    || target.Status != AiSelfMemoryStatus.Active
                    || target.Source != AiSelfMemorySource.Director
                    || target.IsUserLocked)
                {
                    rejectedCount++;
                    continue;
                }

                if (proposal.Operation == AiSelfMemoryProposalOperation.Update)
                {
                    target.SupersedeByDirector(now);
                    dbContext.AiSelfMemories.Add(CreateDirectorMemory(
                        aiAccountId,
                        sourceConversationId,
                        sourceMessage,
                        proposal,
                        now));
                    appliedCount++;
                }
                else
                {
                    target.ArchiveByDirector(now);
                    appliedCount++;
                }
            }

            dbContext.SaveChanges();
            return new AiSelfMemoryProposalApplicationResult
            {
                Status = rejectedCount == 0
                    ? AiSelfMemoryProposalApplicationStatus.Success
                    : AiSelfMemoryProposalApplicationStatus.PartialFailure,
                AppliedCount = appliedCount,
                AlreadyAppliedCount = alreadyAppliedCount,
                RejectedCount = rejectedCount,
                Message = rejectedCount == 0
                    ? "个人记忆建议已处理。"
                    : "部分个人记忆建议没有可验证的消息依据或已不再适用。"
            };
        }
        catch (DbUpdateException)
        {
            return FailedApplication(
                "个人记忆后处理保存失败。",
                proposals.Count);
        }
        catch (SqliteException)
        {
            return FailedApplication(
                "个人记忆后处理保存失败。",
                proposals.Count);
        }
    }

    private static string? ValidateDirectorProposal(
        VocaChatDbContext dbContext,
        Guid aiAccountId,
        AiSelfMemoryProposal proposal,
        HashSet<Guid> claimedTargets)
    {
        if (!Enum.IsDefined(proposal.Operation)
            || !Enum.IsDefined(proposal.Type))
        {
            return "建议包含不支持的操作或记忆类型。";
        }

        if (proposal.Type == AiSelfMemoryType.PersonalFact)
        {
            return "稳定个人事实只能由用户维护。";
        }

        string summary = proposal.Summary?.Trim() ?? string.Empty;
        if (summary.Length == 0 || summary.Length > AiSelfMemory.SummaryMaxLength)
        {
            return "建议摘要为空或超过长度限制。";
        }

        if (proposal.Reason?.Trim().Length is 0 or > 200)
        {
            return "建议原因为空或超过长度限制。";
        }

        if (proposal.Operation == AiSelfMemoryProposalOperation.Add)
        {
            if (proposal.TargetMemoryId is not null)
            {
                return "新增建议不能指定目标记忆。";
            }

            return FindActiveDuplicate(
                    dbContext,
                    aiAccountId,
                    proposal.Type,
                    summary) is null
                ? null
                : "相同的有效个人记忆已经存在。";
        }

        if (proposal.TargetMemoryId is not Guid targetId)
        {
            return "更新或归档建议必须指定目标记忆。";
        }

        if (!claimedTargets.Add(targetId))
        {
            return "同一轮不能多次修改同一条个人记忆。";
        }

        AiSelfMemory? target = dbContext.AiSelfMemories
            .AsNoTracking()
            .SingleOrDefault(memory =>
                memory.Id == targetId
                && memory.AiAccountId == aiAccountId);
        if (target is null || target.Status != AiSelfMemoryStatus.Active)
        {
            return "目标个人记忆不存在或已经失效。";
        }

        if (target.Source == AiSelfMemorySource.User || target.IsUserLocked)
        {
            return "导演不能修改用户来源或用户锁定的个人记忆。";
        }

        return null;
    }

    private static AiSelfMemoryProposal NormalizeProposal(
        AiSelfMemoryProposal proposal)
    {
        return proposal with
        {
            Summary = proposal.Summary.Trim(),
            Reason = proposal.Reason.Trim()
        };
    }

    private static bool IsPersistedSpeakerMessage(
        VocaChatDbContext dbContext,
        Guid aiAccountId,
        Guid conversationId,
        Guid messageId)
    {
        return dbContext.PrivateMessages.Any(message =>
                message.Id == messageId
                && message.PrivateChatId == conversationId
                && message.SenderType == MessageSenderType.AiAccount
                && message.SenderAiAccountId == aiAccountId)
            || dbContext.GroupMessages.Any(message =>
                message.Id == messageId
                && message.GroupChatId == conversationId
                && message.SenderType == MessageSenderType.AiAccount
                && message.SenderAiAccountId == aiAccountId);
    }

    private static AiSelfMemory CreateDirectorMemory(
        Guid aiAccountId,
        Guid sourceConversationId,
        AiPersistedMessageEvidence sourceMessage,
        AiSelfMemoryProposal proposal,
        DateTime createdAt)
    {
        return new AiSelfMemory(
            aiAccountId,
            proposal.Type,
            proposal.Summary,
            AiSelfMemorySource.Director,
            GetDirectorSalience(proposal.Type),
            isUserLocked: false,
            sourceConversationId,
            sourceMessage.MessageId,
            sourceMessage.SentAt,
            validFrom: null,
            validUntil: null,
            createdAt);
    }

    private static int GetDirectorSalience(AiSelfMemoryType type) =>
        type switch
        {
            AiSelfMemoryType.OngoingActivity => 75,
            AiSelfMemoryType.Plan => 75,
            AiSelfMemoryType.Experience => 70,
            AiSelfMemoryType.Preference => 60,
            _ => 60
        };

    private static AiSelfMemoryProposalApplicationResult FailedApplication(
        string message,
        int rejectedCount)
    {
        return new AiSelfMemoryProposalApplicationResult
        {
            Status = AiSelfMemoryProposalApplicationStatus.PersistenceFailed,
            RejectedCount = rejectedCount,
            Message = message
        };
    }

    private static AiSelfMemoryOperationStatus ValidateEditableValues(
        AiSelfMemoryType type,
        string summary,
        int salience,
        DateTime? validFrom,
        DateTime? validUntil,
        out string normalizedSummary,
        out string errorMessage)
    {
        normalizedSummary = string.Empty;

        if (!Enum.IsDefined(type))
        {
            errorMessage = "个人记忆类型无效。";
            return AiSelfMemoryOperationStatus.InvalidType;
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            errorMessage = "个人记忆摘要不能为空。";
            return AiSelfMemoryOperationStatus.InvalidSummary;
        }

        normalizedSummary = summary.Trim();
        if (normalizedSummary.Length > AiSelfMemory.SummaryMaxLength)
        {
            errorMessage = $"个人记忆摘要不能超过 {AiSelfMemory.SummaryMaxLength} 个字符。";
            return AiSelfMemoryOperationStatus.InvalidSummary;
        }

        if (salience is < AiSelfMemory.MinimumSalience
            or > AiSelfMemory.MaximumSalience)
        {
            errorMessage = "个人记忆重要度必须在 1 到 100 之间。";
            return AiSelfMemoryOperationStatus.InvalidSalience;
        }

        if (validFrom is not null
            && validUntil is not null
            && validUntil.Value < validFrom.Value)
        {
            errorMessage = "个人记忆的结束时间不能早于开始时间。";
            return AiSelfMemoryOperationStatus.InvalidTimeRange;
        }

        errorMessage = string.Empty;
        return AiSelfMemoryOperationStatus.Success;
    }

    private static AiSelfMemoryOperationStatus FindTrackedMemory(
        VocaChatDbContext dbContext,
        Guid aiAccountId,
        Guid memoryId,
        out AiSelfMemory? memory,
        out string errorMessage)
    {
        memory = null;

        if (!AccountExists(dbContext, aiAccountId))
        {
            errorMessage = "AI 账号不存在。";
            return AiSelfMemoryOperationStatus.AccountNotFound;
        }

        memory = dbContext.AiSelfMemories.SingleOrDefault(item =>
            item.Id == memoryId && item.AiAccountId == aiAccountId);
        if (memory is null)
        {
            errorMessage = "个人记忆不存在。";
            return AiSelfMemoryOperationStatus.MemoryNotFound;
        }

        errorMessage = string.Empty;
        return AiSelfMemoryOperationStatus.Success;
    }

    private static bool AccountExists(
        VocaChatDbContext dbContext,
        Guid aiAccountId)
    {
        return dbContext.AiAccounts.Any(account => account.Id == aiAccountId);
    }

    private static AiSelfMemory? FindActiveDuplicate(
        VocaChatDbContext dbContext,
        Guid aiAccountId,
        AiSelfMemoryType type,
        string summary)
    {
        return dbContext.AiSelfMemories
            .AsNoTracking()
            .SingleOrDefault(memory =>
                memory.AiAccountId == aiAccountId
                && memory.Type == type
                && memory.Summary == summary
                && memory.Status == AiSelfMemoryStatus.Active);
    }

    private AiSelfMemory? TryReadActiveDuplicate(
        Guid aiAccountId,
        AiSelfMemoryType type,
        string summary)
    {
        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            return FindActiveDuplicate(dbContext, aiAccountId, type, summary);
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                == SqliteUniqueConstraintErrorCode;
    }
}

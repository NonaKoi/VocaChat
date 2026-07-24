using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
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
        return TryCreateUserMemory(
            aiAccountId,
            new AiSelfMemoryWriteData(
                type,
                summary,
                salience,
                isUserLocked,
                occurredAt,
                validFrom,
                validUntil),
            out memory,
            out errorMessage);
    }

    /// <summary>
    /// 创建带有事实键、世界作用域和明确分类的用户个人记忆。
    /// </summary>
    public AiSelfMemoryOperationStatus TryCreateUserMemory(
        Guid aiAccountId,
        AiSelfMemoryWriteData data,
        out AiSelfMemory? memory,
        out string errorMessage)
    {
        memory = null;
        ResolvedAiSelfMemoryWriteData? resolvedData = null;

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiSelfMemoryOperationStatus validationStatus =
                TryResolveWriteData(
                    dbContext,
                    aiAccountId,
                    data,
                    out resolvedData,
                    out errorMessage);
            if (validationStatus != AiSelfMemoryOperationStatus.Success)
            {
                return validationStatus;
            }

            memory = FindActiveFact(
                dbContext,
                aiAccountId,
                resolvedData!.CharacterWorldId,
                resolvedData.FactKey);
            if (memory is not null)
            {
                errorMessage = "该账号在当前世界中已经存在相同事实键的有效个人记忆。";
                return AiSelfMemoryOperationStatus.AlreadyExists;
            }

            DateTime now = DateTime.Now;
            AiSelfMemory newMemory = new(
                aiAccountId,
                resolvedData.Type,
                resolvedData.Summary,
                resolvedData.FactKey,
                resolvedData.FactNature,
                resolvedData.Mutability,
                AiSelfMemoryTrustLevel.UserCanon,
                resolvedData.CharacterWorldId,
                AiSelfMemorySource.User,
                resolvedData.Salience,
                resolvedData.IsUserLocked,
                sourceConversationId: null,
                sourceMessageId: null,
                supersedesMemoryId: null,
                resolvedData.OccurredAt,
                resolvedData.ValidFrom,
                resolvedData.ValidUntil,
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
            memory = resolvedData is null
                ? null
                : TryReadActiveFact(
                    aiAccountId,
                    resolvedData.CharacterWorldId,
                    resolvedData.FactKey);
            errorMessage = "该账号在当前世界中已经存在相同事实键的有效个人记忆。";
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

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            Guid? currentWorldId = dbContext.AiAccounts
                .AsNoTracking()
                .Where(account => account.Id == aiAccountId)
                .Select(account => (Guid?)account.CharacterWorldId)
                .SingleOrDefault();
            if (currentWorldId is null)
            {
                memories = Array.Empty<AiSelfMemory>();
                errorMessage = "AI 账号不存在。";
                return AiSelfMemoryOperationStatus.AccountNotFound;
            }

            memories = dbContext.AiSelfMemories
                .AsNoTracking()
                .Where(memory =>
                    memory.AiAccountId == aiAccountId
                    && memory.CharacterWorldId == currentWorldId.Value
                    && memory.Status == AiSelfMemoryStatus.Active)
                .OrderByDescending(memory => memory.IsUserLocked)
                .ThenBy(memory => memory.TrustLevel)
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
            memories = Array.Empty<AiSelfMemory>();
            errorMessage = "个人记忆暂时无法读取，请稍后重试。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 为消息保存后的语义判断读取当前角色世界及少量有效个人记忆。
    /// </summary>
    public AiSelfMemoryOperationStatus TryGetSemanticJudgmentContext(
        Guid aiAccountId,
        Guid sourceConversationId,
        IReadOnlyList<AiPersistedMessageEvidence> savedMessages,
        out AiSelfMemorySemanticContext? context,
        out IReadOnlyList<AiPersistedMessageEvidence> verifiedMessages,
        out string errorMessage)
    {
        context = null;
        verifiedMessages = Array.Empty<AiPersistedMessageEvidence>();

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiAccount? account = dbContext.AiAccounts
                .AsNoTracking()
                .Include(item => item.CharacterWorld)
                .SingleOrDefault(item => item.Id == aiAccountId);
            if (account is null || account.CharacterWorld is null)
            {
                errorMessage = "AI 账号或角色世界不存在。";
                return AiSelfMemoryOperationStatus.AccountNotFound;
            }

            verifiedMessages = savedMessages
                .Where(message => IsPersistedSpeakerMessage(
                    dbContext,
                    aiAccountId,
                    sourceConversationId,
                    message.MessageId))
                .ToList()
                .AsReadOnly();
            if (verifiedMessages.Count == 0)
            {
                errorMessage = "没有找到可验证的正式 AI 消息来源。";
                return AiSelfMemoryOperationStatus.MemoryNotFound;
            }

            IReadOnlyList<AiConversationSelfMemory> memories =
                dbContext.AiSelfMemories
                    .AsNoTracking()
                    .Where(memory =>
                        memory.AiAccountId == aiAccountId
                        && memory.CharacterWorldId
                            == account.CharacterWorldId
                        && memory.Status == AiSelfMemoryStatus.Active)
                    .OrderByDescending(memory => memory.IsUserLocked)
                    .ThenBy(memory => memory.TrustLevel)
                    .ThenByDescending(memory => memory.Salience)
                    .ThenByDescending(memory => memory.UpdatedAt)
                    .ThenBy(memory => memory.Id)
                    .Take(MaximumContextMemoryCount)
                    .Select(memory => new AiConversationSelfMemory(
                        memory.Id,
                        memory.AiAccountId,
                        memory.Type,
                        memory.Summary,
                        memory.FactKey,
                        memory.FactNature,
                        memory.Mutability,
                        memory.TrustLevel,
                        memory.CharacterWorldId,
                        memory.Source,
                        memory.Salience,
                        memory.IsUserLocked,
                        memory.OccurredAt,
                        memory.UpdatedAt))
                    .ToList()
                    .AsReadOnly();

            context = new AiSelfMemorySemanticContext(
                account.CharacterWorld.Name,
                account.CharacterWorld.Description,
                memories);
            errorMessage = string.Empty;
            return AiSelfMemoryOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "个人记忆语义判断上下文暂时无法读取。";
            return AiSelfMemoryOperationStatus.PersistenceFailed;
        }
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
        return TryUpdateUserMemory(
            aiAccountId,
            memoryId,
            new AiSelfMemoryWriteData(
                type,
                summary,
                salience,
                isUserLocked,
                occurredAt,
                validFrom,
                validUntil),
            out memory,
            out errorMessage);
    }

    /// <summary>
    /// 以新版本保存用户修订，旧记录保留为 Superseded。
    /// </summary>
    public AiSelfMemoryOperationStatus TryUpdateUserMemory(
        Guid aiAccountId,
        Guid memoryId,
        AiSelfMemoryWriteData data,
        out AiSelfMemory? memory,
        out string errorMessage)
    {
        memory = null;
        ResolvedAiSelfMemoryWriteData? resolvedData = null;

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiSelfMemoryOperationStatus validationStatus =
                TryResolveWriteData(
                    dbContext,
                    aiAccountId,
                    data,
                    out resolvedData,
                    out errorMessage);
            if (validationStatus != AiSelfMemoryOperationStatus.Success)
            {
                return validationStatus;
            }
            ResolvedAiSelfMemoryWriteData values = resolvedData!;

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

            if (storedMemory!.Status == AiSelfMemoryStatus.Superseded)
            {
                errorMessage = "已替代的个人记忆只能作为历史查看。";
                return AiSelfMemoryOperationStatus.InvalidStatus;
            }

            if (storedMemory.Status == AiSelfMemoryStatus.Archived)
            {
                errorMessage = "已归档的个人记忆需要恢复后才能修订。";
                return AiSelfMemoryOperationStatus.InvalidStatus;
            }

            AiSelfMemory? conflictingMemory = dbContext.AiSelfMemories
                .AsNoTracking()
                .SingleOrDefault(item =>
                    item.Id != storedMemory.Id
                    && item.AiAccountId == aiAccountId
                    && item.CharacterWorldId
                        == values.CharacterWorldId
                    && item.FactKey == values.FactKey
                    && item.Status == AiSelfMemoryStatus.Active);
            if (conflictingMemory is not null)
            {
                memory = conflictingMemory;
                errorMessage = "该账号在目标世界中已经存在相同事实键的有效个人记忆。";
                return AiSelfMemoryOperationStatus.AlreadyExists;
            }

            DateTime now = DateTime.Now;
            using var transaction = dbContext.Database.BeginTransaction();
            storedMemory.SupersedeByUser(now);
            dbContext.SaveChanges();

            AiSelfMemory replacement = new(
                aiAccountId,
                values.Type,
                values.Summary,
                values.FactKey,
                values.FactNature,
                values.Mutability,
                AiSelfMemoryTrustLevel.UserCanon,
                values.CharacterWorldId,
                AiSelfMemorySource.User,
                values.Salience,
                values.IsUserLocked,
                sourceConversationId: null,
                sourceMessageId: null,
                supersedesMemoryId: storedMemory.Id,
                values.OccurredAt,
                values.ValidFrom,
                values.ValidUntil,
                now);
            dbContext.AiSelfMemories.Add(replacement);
            dbContext.SaveChanges();
            transaction.Commit();

            memory = replacement;
            errorMessage = string.Empty;
            return AiSelfMemoryOperationStatus.Success;
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            memory = resolvedData is null
                ? null
                : TryReadActiveFact(
                    aiAccountId,
                    resolvedData.CharacterWorldId,
                    resolvedData.FactKey);
            errorMessage = "该账号在目标世界中已经存在相同事实键的有效个人记忆。";
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
    /// 允许用户归档或恢复当前版本；已替代版本只作为历史保留。
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

            if (storedMemory!.Status == AiSelfMemoryStatus.Superseded)
            {
                errorMessage = "已替代的个人记忆只能作为历史查看。";
                return AiSelfMemoryOperationStatus.InvalidStatus;
            }

            if (storedMemory.Status != status)
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
            Guid characterWorldId = GetAccountCharacterWorldId(
                dbContext,
                aiAccountId);

            for (int index = 0; index < proposals.Count; index++)
            {
                AiSelfMemoryProposal proposal =
                    NormalizeProposal(proposals[index]);
                string? rejectionReason = index >= MaximumDirectorProposalCount
                    ? $"每轮最多接受 {MaximumDirectorProposalCount} 项个人记忆建议。"
                    : ValidateDirectorProposal(
                        dbContext,
                        aiAccountId,
                        characterWorldId,
                        proposal,
                        claimedTargets);
                bool isAccepted = rejectionReason is null;
                if (isAccepted)
                {
                    accepted.Add(proposal);
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
    /// 在 AI 消息正式保存且语义判断完成后应用导演建议。
    /// 所有模型决定都会在写库前重新经过确定性业务规则。
    /// </summary>
    public AiSelfMemoryProposalApplicationResult ApplyDirectorProposals(
        Guid aiAccountId,
        Guid sourceConversationId,
        IReadOnlyList<AiSelfMemoryProposal> proposals,
        AiSelfMemorySemanticJudgmentResult judgmentResult,
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
            Guid characterWorldId = GetAccountCharacterWorldId(
                dbContext,
                aiAccountId);

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
            int acceptedCount = 0;
            int supersededCount = 0;
            int archivedCount = 0;
            int pendingCount = 0;
            int rejectedCount = 0;
            DateTime now = DateTime.Now;
            HashSet<Guid> claimedTargets = new();
            using var transaction = dbContext.Database.BeginTransaction();

            IReadOnlyDictionary<int, AiSelfMemorySemanticDecision>
                decisionsByIndex = judgmentResult.Decisions
                    .GroupBy(decision => decision.ProposalIndex)
                    .ToDictionary(group => group.Key, group => group.First());

            for (int proposalIndex = 0;
                 proposalIndex < Math.Min(
                     proposals.Count,
                     MaximumDirectorProposalCount);
                 proposalIndex++)
            {
                AiSelfMemoryProposal proposal =
                    NormalizeProposal(proposals[proposalIndex]);
                if (!decisionsByIndex.TryGetValue(
                        proposalIndex,
                        out AiSelfMemorySemanticDecision? decision))
                {
                    pendingCount++;
                    continue;
                }

                if (decision.Outcome == AiSelfMemorySemanticOutcome.Pending)
                {
                    pendingCount++;
                    continue;
                }

                if (decision.Outcome == AiSelfMemorySemanticOutcome.Reject)
                {
                    rejectedCount++;
                    continue;
                }

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

                string? proposalError = ValidateDirectorProposal(
                    dbContext,
                    aiAccountId,
                    characterWorldId,
                    proposal,
                    claimedTargets);
                if (proposalError is not null)
                {
                    rejectedCount++;
                    continue;
                }

                string? decisionError = ValidateSemanticDecision(
                    dbContext,
                    aiAccountId,
                    characterWorldId,
                    proposal,
                    decision);
                if (decisionError is not null)
                {
                    rejectedCount++;
                    continue;
                }

                string factKey = NormalizeFactKey(decision.FactKey);
                if (decision.Outcome == AiSelfMemorySemanticOutcome.Accept)
                {
                    AiSelfMemory? existingFact = FindActiveFact(
                        dbContext,
                        aiAccountId,
                        characterWorldId,
                        factKey);
                    if (existingFact is not null)
                    {
                        if (existingFact.Type == proposal.Type
                            && string.Equals(
                                existingFact.Summary,
                                proposal.Summary,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyAppliedCount++;
                        }
                        else
                        {
                            rejectedCount++;
                        }
                        continue;
                    }

                    dbContext.AiSelfMemories.Add(CreateDirectorMemory(
                        aiAccountId,
                        sourceConversationId,
                        sourceMessage,
                        proposal,
                        decision,
                        characterWorldId,
                        supersededMemory: null,
                        now));
                    appliedCount++;
                    acceptedCount++;
                    continue;
                }

                AiSelfMemory? target = decision.TargetMemoryId is Guid targetId
                    ? dbContext.AiSelfMemories.SingleOrDefault(memory =>
                        memory.Id == targetId
                        && memory.AiAccountId == aiAccountId)
                    : null;
                if (target is null
                    || target.Status != AiSelfMemoryStatus.Active
                    || target.Source != AiSelfMemorySource.Director
                    || target.IsUserLocked
                    || target.CharacterWorldId != characterWorldId
                    || target.Mutability == AiSelfMemoryMutability.Immutable
                    || !string.Equals(
                        target.FactKey,
                        factKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    rejectedCount++;
                    continue;
                }

                if (decision.Outcome
                    == AiSelfMemorySemanticOutcome.Supersede)
                {
                    target.SupersedeByDirector(now);
                    dbContext.SaveChanges();
                    dbContext.AiSelfMemories.Add(CreateDirectorMemory(
                        aiAccountId,
                        sourceConversationId,
                        sourceMessage,
                        proposal,
                        decision,
                        target.CharacterWorldId,
                        target,
                        now));
                    appliedCount++;
                    supersededCount++;
                }
                else
                {
                    target.ArchiveByDirector(now);
                    appliedCount++;
                    archivedCount++;
                }
            }

            dbContext.SaveChanges();
            transaction.Commit();
            return new AiSelfMemoryProposalApplicationResult
            {
                Status = rejectedCount == 0 && pendingCount == 0
                    ? AiSelfMemoryProposalApplicationStatus.Success
                    : AiSelfMemoryProposalApplicationStatus.PartialFailure,
                AppliedCount = appliedCount,
                AlreadyAppliedCount = alreadyAppliedCount,
                AcceptedCount = acceptedCount,
                SupersededCount = supersededCount,
                ArchivedCount = archivedCount,
                PendingCount = pendingCount,
                RejectedCount = rejectedCount,
                Message = rejectedCount == 0 && pendingCount == 0
                    ? "个人记忆建议已处理。"
                    : "部分个人记忆建议被拒绝或保留为待确认。"
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

    private static string? ValidateSemanticDecision(
        VocaChatDbContext dbContext,
        Guid aiAccountId,
        Guid characterWorldId,
        AiSelfMemoryProposal proposal,
        AiSelfMemorySemanticDecision decision)
    {
        if (!Enum.IsDefined(decision.Outcome)
            || !Enum.IsDefined(decision.FactNature)
            || !Enum.IsDefined(decision.Mutability))
        {
            return "语义判断包含不支持的结果或事实分类。";
        }

        if (proposal.SubjectAiAccountId != aiAccountId
            || proposal.CharacterWorldId != characterWorldId)
        {
            return "候选的主体或角色世界已经不再匹配当前账号。";
        }

        string factKey = NormalizeFactKey(decision.FactKey ?? string.Empty);
        if (factKey.Length == 0
            || factKey.Length > AiSelfMemory.FactKeyMaxLength)
        {
            return "语义判断的事实键无效。";
        }

        if (decision.Mutability == AiSelfMemoryMutability.Ephemeral)
        {
            return "没有有效期的短期状态不能进入长期个人记忆。";
        }

        if (decision.FactNature == AiSelfMemoryFactNature.Objective
            && decision.Mutability == AiSelfMemoryMutability.Immutable)
        {
            return "导演不能通过语义判断创建恒定客观事实。";
        }

        if (decision.FactNature == AiSelfMemoryFactNature.Subjective
            && decision.Mutability == AiSelfMemoryMutability.Immutable)
        {
            return "主观内容不能作为恒定不可变事实保存。";
        }

        if (decision.Outcome == AiSelfMemorySemanticOutcome.Accept)
        {
            return proposal.Operation == AiSelfMemoryProposalOperation.Add
                    && decision.TargetMemoryId is null
                ? null
                : "Accept 只能用于没有目标记忆的新增候选。";
        }

        if (decision.Outcome == AiSelfMemorySemanticOutcome.Supersede)
        {
            if (proposal.Operation == AiSelfMemoryProposalOperation.Archive)
            {
                return "归档候选不能被改为替代操作。";
            }

            if (decision.TargetMemoryId is not Guid supersededId)
            {
                return "替代决定缺少目标记忆。";
            }

            if (proposal.TargetMemoryId is Guid proposedTargetId
                && proposedTargetId != supersededId)
            {
                return "语义判断不能替换导演指定的其他目标记忆。";
            }

            return dbContext.AiSelfMemories.AsNoTracking().Any(memory =>
                    memory.Id == supersededId
                    && memory.AiAccountId == aiAccountId
                    && memory.CharacterWorldId == characterWorldId
                    && memory.FactKey == factKey
                    && memory.Status == AiSelfMemoryStatus.Active)
                ? null
                : "替代决定没有指向同一事实键的有效记忆。";
        }

        if (decision.Outcome == AiSelfMemorySemanticOutcome.Archive)
        {
            if (proposal.Operation != AiSelfMemoryProposalOperation.Archive
                || decision.TargetMemoryId is not Guid archivedId
                || proposal.TargetMemoryId != archivedId)
            {
                return "Archive 必须与导演明确指定的归档目标一致。";
            }

            return dbContext.AiSelfMemories.AsNoTracking().Any(memory =>
                    memory.Id == archivedId
                    && memory.AiAccountId == aiAccountId
                    && memory.CharacterWorldId == characterWorldId
                    && memory.FactKey == factKey
                    && memory.Status == AiSelfMemoryStatus.Active)
                ? null
                : "归档决定没有指向同一事实键的有效记忆。";
        }

        return "语义判断结果不能触发记忆写入。";
    }

    private static string? ValidateDirectorProposal(
        VocaChatDbContext dbContext,
        Guid aiAccountId,
        Guid characterWorldId,
        AiSelfMemoryProposal proposal,
        HashSet<Guid> claimedTargets)
    {
        if (!Enum.IsDefined(proposal.Operation)
            || !Enum.IsDefined(proposal.Type)
            || !Enum.IsDefined(proposal.FactNature)
            || !Enum.IsDefined(proposal.Mutability))
        {
            return "建议包含不支持的操作、记忆类型或事实分类。";
        }

        if (proposal.SubjectAiAccountId != aiAccountId)
        {
            return "建议主体不是当前实际发言账号。";
        }

        if (proposal.CharacterWorldId != characterWorldId)
        {
            return "建议世界不是当前发言账号的角色世界。";
        }

        if (proposal.Type == AiSelfMemoryType.PersonalFact)
        {
            return "稳定个人事实只能由用户维护。";
        }

        if (proposal.FactNature == AiSelfMemoryFactNature.Objective
            && proposal.Mutability == AiSelfMemoryMutability.Immutable)
        {
            return "恒定客观事实只能由用户确认和维护。";
        }

        if (proposal.FactNature == AiSelfMemoryFactNature.Subjective
            && proposal.Mutability == AiSelfMemoryMutability.Immutable)
        {
            return "主观内容不能标记为恒定不可变事实。";
        }

        string factKey = NormalizeFactKey(proposal.FactKey ?? string.Empty);
        if (factKey.Length == 0
            || factKey.Length > AiSelfMemory.FactKeyMaxLength)
        {
            return "建议事实键为空或超过长度限制。";
        }

        string summary = proposal.Summary?.Trim() ?? string.Empty;
        if (summary.Length == 0 || summary.Length > AiSelfMemory.SummaryMaxLength)
        {
            return "建议摘要为空或超过长度限制。";
        }

        if (AiNarrativeConsistencyPolicy
                .RequiresReliableExternalStatusSource(characterWorldId)
            && AiNarrativeConsistencyPolicy
                .ContainsDefinitiveExternalStatus(summary))
        {
            return "导演记忆不能把没有独立来源的营业、活动或经营信息固化为个人记忆。";
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

            AiSelfMemory? exactDuplicate = FindActiveDuplicate(
                    dbContext,
                    aiAccountId,
                    characterWorldId,
                    proposal.Type,
                    summary);
            if (exactDuplicate is not null)
            {
                return "相同的有效个人记忆已经存在。";
            }

            AiSelfMemory? existingFact = FindActiveFact(
                dbContext,
                aiAccountId,
                characterWorldId,
                factKey);
            if (existingFact is null)
            {
                return null;
            }

            return existingFact.Source == AiSelfMemorySource.User
                    || existingFact.IsUserLocked
                    || existingFact.Mutability
                        == AiSelfMemoryMutability.Immutable
                ? "相同事实键已经由用户正典、锁定内容或恒定事实占用。"
                : null;
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

        if (target.CharacterWorldId != characterWorldId)
        {
            return "导演不能修改账号其他角色世界中的个人记忆。";
        }

        if (!string.Equals(
                target.FactKey,
                factKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return "更新或归档建议的事实键与目标记忆不一致。";
        }

        if (target.Source == AiSelfMemorySource.User || target.IsUserLocked)
        {
            return "导演不能修改用户来源或用户锁定的个人记忆。";
        }

        if (target.Mutability == AiSelfMemoryMutability.Immutable)
        {
            return "导演不能修改或归档恒定个人事实。";
        }

        return null;
    }

    private static AiSelfMemoryProposal NormalizeProposal(
        AiSelfMemoryProposal proposal)
    {
        return proposal with
        {
            FactKey = NormalizeFactKey(proposal.FactKey ?? string.Empty),
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
        AiSelfMemorySemanticDecision decision,
        Guid characterWorldId,
        AiSelfMemory? supersededMemory,
        DateTime createdAt)
    {
        return new AiSelfMemory(
            aiAccountId,
            proposal.Type,
            proposal.Summary,
            supersededMemory?.FactKey
                ?? NormalizeFactKey(decision.FactKey),
            decision.FactNature,
            decision.Mutability,
            GetDirectorTrustLevel(decision.FactNature),
            characterWorldId,
            AiSelfMemorySource.Director,
            GetDirectorSalience(proposal.Type),
            isUserLocked: false,
            sourceConversationId,
            sourceMessage.MessageId,
            supersededMemory?.Id,
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

    private static AiSelfMemoryOperationStatus TryResolveWriteData(
        VocaChatDbContext dbContext,
        Guid aiAccountId,
        AiSelfMemoryWriteData data,
        out ResolvedAiSelfMemoryWriteData? resolvedData,
        out string errorMessage)
    {
        resolvedData = null;

        if (data is null)
        {
            errorMessage = "个人记忆内容不能为空。";
            return AiSelfMemoryOperationStatus.InvalidSummary;
        }

        AiSelfMemoryOperationStatus valueStatus = ValidateEditableValues(
            data.Type,
            data.Summary,
            data.Salience,
            data.ValidFrom,
            data.ValidUntil,
            out string normalizedSummary,
            out errorMessage);
        if (valueStatus != AiSelfMemoryOperationStatus.Success)
        {
            return valueStatus;
        }

        Guid? currentWorldId = dbContext.AiAccounts
            .AsNoTracking()
            .Where(account => account.Id == aiAccountId)
            .Select(account => (Guid?)account.CharacterWorldId)
            .SingleOrDefault();
        if (currentWorldId is null)
        {
            errorMessage = "AI 账号不存在。";
            return AiSelfMemoryOperationStatus.AccountNotFound;
        }

        Guid characterWorldId = data.CharacterWorldId ?? currentWorldId.Value;
        if (!dbContext.CharacterWorlds.Any(world =>
                world.Id == characterWorldId))
        {
            errorMessage = "角色世界不存在。";
            return AiSelfMemoryOperationStatus.CharacterWorldNotFound;
        }

        AiSelfMemoryFactNature factNature = data.FactNature
            ?? GetDefaultFactNature(data.Type);
        AiSelfMemoryMutability mutability = data.Mutability
            ?? GetDefaultMutability(data.Type);
        if (!Enum.IsDefined(factNature) || !Enum.IsDefined(mutability))
        {
            errorMessage = "个人记忆的事实性质或可变性无效。";
            return AiSelfMemoryOperationStatus.InvalidClassification;
        }

        if (mutability == AiSelfMemoryMutability.Ephemeral
            && data.ValidUntil is null)
        {
            errorMessage = "短期状态必须设置结束时间。";
            return AiSelfMemoryOperationStatus.InvalidTimeRange;
        }

        string factKey = string.IsNullOrWhiteSpace(data.FactKey)
            ? CreateAutomaticFactKey(data.Type, normalizedSummary)
            : NormalizeFactKey(data.FactKey);
        if (factKey.Length == 0
            || factKey.Length > AiSelfMemory.FactKeyMaxLength)
        {
            errorMessage =
                $"事实键不能为空，且不能超过 {AiSelfMemory.FactKeyMaxLength} 个字符。";
            return AiSelfMemoryOperationStatus.InvalidFactKey;
        }

        resolvedData = new ResolvedAiSelfMemoryWriteData(
            data.Type,
            normalizedSummary,
            factKey,
            factNature,
            mutability,
            characterWorldId,
            data.Salience,
            data.IsUserLocked,
            data.OccurredAt,
            data.ValidFrom,
            data.ValidUntil);
        errorMessage = string.Empty;
        return AiSelfMemoryOperationStatus.Success;
    }

    private static AiSelfMemoryFactNature GetDefaultFactNature(
        AiSelfMemoryType type) =>
        type switch
        {
            AiSelfMemoryType.Preference
                => AiSelfMemoryFactNature.Subjective,
            AiSelfMemoryType.Experience
                => AiSelfMemoryFactNature.Narrative,
            _ => AiSelfMemoryFactNature.Objective
        };

    private static AiSelfMemoryMutability GetDefaultMutability(
        AiSelfMemoryType type) =>
        type switch
        {
            AiSelfMemoryType.PersonalFact
                => AiSelfMemoryMutability.Immutable,
            AiSelfMemoryType.Preference
                => AiSelfMemoryMutability.Evolving,
            AiSelfMemoryType.Experience
                => AiSelfMemoryMutability.Immutable,
            _ => AiSelfMemoryMutability.Mutable
        };

    private static AiSelfMemoryTrustLevel GetDirectorTrustLevel(
        AiSelfMemoryFactNature factNature) =>
        factNature == AiSelfMemoryFactNature.Subjective
            ? AiSelfMemoryTrustLevel.SubjectiveState
            : AiSelfMemoryTrustLevel.NarrativeCandidate;

    private static Guid GetAccountCharacterWorldId(
        VocaChatDbContext dbContext,
        Guid aiAccountId)
    {
        return dbContext.AiAccounts
            .Where(account => account.Id == aiAccountId)
            .Select(account => account.CharacterWorldId)
            .Single();
    }

    private static string NormalizeFactKey(string factKey)
    {
        return factKey.Trim().ToLowerInvariant();
    }

    private static string CreateAutomaticFactKey(
        AiSelfMemoryType type,
        string summary)
    {
        string source =
            $"{type}:{summary.Trim().ToLowerInvariant()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        string shortHash = Convert.ToHexString(hash[..8]).ToLowerInvariant();
        return $"auto.{type.ToString().ToLowerInvariant()}.{shortHash}";
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
        Guid characterWorldId,
        AiSelfMemoryType type,
        string summary)
    {
        return dbContext.AiSelfMemories
            .AsNoTracking()
            .SingleOrDefault(memory =>
                memory.AiAccountId == aiAccountId
                && memory.CharacterWorldId == characterWorldId
                && memory.Type == type
                && memory.Summary == summary
                && memory.Status == AiSelfMemoryStatus.Active);
    }

    private static AiSelfMemory? FindActiveFact(
        VocaChatDbContext dbContext,
        Guid aiAccountId,
        Guid characterWorldId,
        string factKey)
    {
        return dbContext.AiSelfMemories
            .AsNoTracking()
            .SingleOrDefault(memory =>
                memory.AiAccountId == aiAccountId
                && memory.CharacterWorldId == characterWorldId
                && memory.FactKey == factKey
                && memory.Status == AiSelfMemoryStatus.Active);
    }

    private AiSelfMemory? TryReadActiveFact(
        Guid aiAccountId,
        Guid characterWorldId,
        string factKey)
    {
        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            return FindActiveFact(
                dbContext,
                aiAccountId,
                characterWorldId,
                factKey);
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

    private sealed record ResolvedAiSelfMemoryWriteData(
        AiSelfMemoryType Type,
        string Summary,
        string FactKey,
        AiSelfMemoryFactNature FactNature,
        AiSelfMemoryMutability Mutability,
        Guid CharacterWorldId,
        int Salience,
        bool IsUserLocked,
        DateTime? OccurredAt,
        DateTime? ValidFrom,
        DateTime? ValidUntil);
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责世界知识、正式消息证据和群消息接收者快照的验证与持久化。
/// </summary>
public sealed class AiWorldKnowledgeService
{
    private const int MaximumQueryCount = 100;
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiWorldKnowledgeService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 第一次处理一条群消息时保存当时所有 AI 群成员的可见性快照。
    /// 已经存在快照时直接返回原记录，不按当前成员重新计算历史。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryRecordGroupMessageAudience(
        Guid groupMessageId,
        out IReadOnlyList<GroupMessageAudience> audience,
        out string errorMessage)
    {
        audience = Array.Empty<GroupMessageAudience>();

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            GroupMessage? message = dbContext.GroupMessages
                .AsNoTracking()
                .SingleOrDefault(item => item.Id == groupMessageId);

            if (message is null)
            {
                errorMessage = "群消息不存在，不能保存接收者快照。";
                return AiWorldKnowledgeOperationStatus.GroupMessageNotFound;
            }

            List<GroupMessageAudience> existing = dbContext
                .GroupMessageAudience
                .AsNoTracking()
                .Where(item => item.GroupMessageId == groupMessageId)
                .OrderBy(item => item.AiAccountId)
                .ToList();

            if (existing.Count > 0)
            {
                audience = existing.AsReadOnly();
                errorMessage = string.Empty;
                return AiWorldKnowledgeOperationStatus.AlreadyExists;
            }

            List<Guid> memberIds = dbContext.GroupChats
                .AsNoTracking()
                .Where(groupChat => groupChat.Id == message.GroupChatId)
                .SelectMany(groupChat => groupChat.Members)
                .Select(member => member.Id)
                .OrderBy(id => id)
                .ToList();
            DateTime visibleAt = message.SentAt;
            List<GroupMessageAudience> newAudience = memberIds
                .Select(aiAccountId => new GroupMessageAudience(
                    groupMessageId,
                    aiAccountId,
                    visibleAt))
                .ToList();

            dbContext.GroupMessageAudience.AddRange(newAudience);
            dbContext.SaveChanges();
            audience = newAudience.AsReadOnly();
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }
        catch (DbUpdateException)
        {
            IReadOnlyList<GroupMessageAudience> existing =
                ReadGroupMessageAudience(groupMessageId);

            if (existing.Count > 0)
            {
                audience = existing;
                errorMessage = string.Empty;
                return AiWorldKnowledgeOperationStatus.AlreadyExists;
            }

            errorMessage = "群消息接收者快照暂时无法保存。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "群消息接收者快照暂时无法保存。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 返回一条群消息保存时的 AI 接收者快照。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryGetGroupMessageAudience(
        Guid groupMessageId,
        out IReadOnlyList<GroupMessageAudience> audience,
        out string errorMessage)
    {
        audience = Array.Empty<GroupMessageAudience>();

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!dbContext.GroupMessages
                    .AsNoTracking()
                    .Any(message => message.Id == groupMessageId))
            {
                errorMessage = "群消息不存在。";
                return AiWorldKnowledgeOperationStatus.GroupMessageNotFound;
            }

            audience = dbContext.GroupMessageAudience
                .AsNoTracking()
                .Where(item => item.GroupMessageId == groupMessageId)
                .OrderBy(item => item.AiAccountId)
                .ToList()
                .AsReadOnly();
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "群消息接收者快照暂时无法读取。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 从一条对 Owner 真实可见的正式消息创建世界知识和来源证据。
    /// 同义内容归入已有知识；主观变化形成新版本；恒定客观冲突保留为候选。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryCreateKnowledge(
        AiWorldKnowledgeWriteData data,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        string evidenceSummary,
        out AiWorldKnowledge? knowledge,
        out string errorMessage)
    {
        knowledge = null;
        AiWorldKnowledgeOperationStatus inputStatus = ValidateInput(
            data,
            evidenceSummary,
            out string normalizedKnowledgeKey,
            out string normalizedSummary,
            out string normalizedEvidenceSummary,
            out errorMessage);

        if (inputStatus != AiWorldKnowledgeOperationStatus.Success)
        {
            return inputStatus;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiWorldKnowledgeOperationStatus scopeStatus =
                ValidateKnowledgeScope(
                    dbContext,
                    data,
                    out errorMessage);

            if (scopeStatus != AiWorldKnowledgeOperationStatus.Success)
            {
                return scopeStatus;
            }

            if (!AiConversationEvidenceValidator.TryValidate(
                    dbContext,
                    data.OwnerAiAccountId,
                    sourcePrivateMessageId,
                    sourceGroupMessageId,
                    out AiConversationEvidenceValidationStatus sourceStatus,
                    out ValidatedConversationEvidence? source,
                    out errorMessage))
            {
                return MapSourceFailure(sourceStatus);
            }

            knowledge = FindActiveKnowledge(
                dbContext,
                data.OwnerAiAccountId,
                data.SubjectCharacterWorldId,
                data.SubjectAiAccountId,
                normalizedKnowledgeKey);
            knowledge ??= FindEquivalentActiveKnowledge(
                dbContext,
                data.OwnerAiAccountId,
                data.SubjectCharacterWorldId,
                data.SubjectAiAccountId,
                normalizedSummary);
            bool knowledgeAlreadyExists = knowledge is not null;
            AiWorldKnowledgeOperationStatus successStatus =
                knowledgeAlreadyExists
                    ? AiWorldKnowledgeOperationStatus.EvidenceAdded
                    : AiWorldKnowledgeOperationStatus.Success;

            if (knowledge is null)
            {
                knowledge = new AiWorldKnowledge(
                    data.OwnerAiAccountId,
                    data.SubjectCharacterWorldId,
                    data.SubjectAiAccountId,
                    normalizedKnowledgeKey,
                    normalizedSummary,
                    data.FactNature,
                    data.Mutability,
                    data.TrustLevel,
                    data.Salience,
                    data.IsUserLocked,
                    source!.ObservedAt);
                dbContext.AiWorldKnowledge.Add(knowledge);
            }
            else if (!AreEquivalentSummaries(
                         knowledge.Summary,
                         normalizedSummary))
            {
                DateTime observedAt = source!.ObservedAt;
                if (ShouldCreateConflictCandidate(knowledge, data))
                {
                    AiWorldKnowledge? existingConflict =
                        FindEquivalentConflictCandidate(
                            dbContext,
                            data.OwnerAiAccountId,
                            data.SubjectCharacterWorldId,
                            data.SubjectAiAccountId,
                            normalizedSummary);
                    if (existingConflict is not null)
                    {
                        knowledge = existingConflict;
                        successStatus =
                            AiWorldKnowledgeOperationStatus.EvidenceAdded;
                    }
                    else
                    {
                        knowledge = CreateConflictCandidate(
                            dbContext,
                            data,
                            normalizedKnowledgeKey,
                            normalizedSummary,
                            observedAt);
                        successStatus =
                            AiWorldKnowledgeOperationStatus
                                .ConflictCandidateCreated;
                    }
                }
                else if (ShouldSupersede(knowledge, data))
                {
                    knowledge.MarkAsSuperseded(observedAt);
                    knowledge = new AiWorldKnowledge(
                        data.OwnerAiAccountId,
                        data.SubjectCharacterWorldId,
                        data.SubjectAiAccountId,
                        normalizedKnowledgeKey,
                        normalizedSummary,
                        data.FactNature,
                        data.Mutability,
                        data.TrustLevel,
                        data.Salience,
                        data.IsUserLocked,
                        observedAt);
                    dbContext.AiWorldKnowledge.Add(knowledge);
                    successStatus =
                        AiWorldKnowledgeOperationStatus
                            .KnowledgeSuperseded;
                }
            }

            Guid knowledgeId = knowledge.Id;
            bool evidenceExists = dbContext.AiWorldKnowledgeEvidence
                .AsNoTracking()
                .Any(item =>
                    item.AiWorldKnowledgeId == knowledgeId
                    && ((item.SourcePrivateMessageId
                                == source!.SourcePrivateMessageId
                            && item.SourcePrivateMessageId != null)
                        || (item.SourceGroupMessageId
                                == source.SourceGroupMessageId
                            && item.SourceGroupMessageId != null)));

            if (evidenceExists)
            {
                errorMessage = string.Empty;
                return AiWorldKnowledgeOperationStatus.AlreadyExists;
            }

            AiWorldKnowledgeEvidence evidence = new(
                knowledge.Id,
                source!.SourceType,
                source.SourceAiAccountId,
                source.SourcePrivateMessageId,
                source.SourceGroupMessageId,
                normalizedEvidenceSummary,
                source.ObservedAt);
            dbContext.AiWorldKnowledgeEvidence.Add(evidence);
            PromoteTrustFromIndependentSources(
                dbContext,
                knowledge,
                source);
            dbContext.SaveChanges();
            errorMessage = string.Empty;
            return successStatus;
        }
        catch (DbUpdateException)
        {
            errorMessage = "世界知识暂时无法保存。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "世界知识暂时无法保存。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 返回 Owner 在指定世界作用域中的少量有效知识。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryGetActiveKnowledge(
        Guid ownerAiAccountId,
        Guid subjectCharacterWorldId,
        Guid? subjectAiAccountId,
        int maximumCount,
        out IReadOnlyList<AiWorldKnowledge> knowledge,
        out string errorMessage)
    {
        knowledge = Array.Empty<AiWorldKnowledge>();

        if (maximumCount is < 1 or > MaximumQueryCount)
        {
            errorMessage = $"单次最多只能查询 1 到 {MaximumQueryCount} 条世界知识。";
            return AiWorldKnowledgeOperationStatus.InvalidLimit;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!dbContext.AiAccounts
                    .AsNoTracking()
                    .Any(account => account.Id == ownerAiAccountId))
            {
                errorMessage = "世界知识所有者账号不存在。";
                return AiWorldKnowledgeOperationStatus.AccountNotFound;
            }

            if (!dbContext.CharacterWorlds
                    .AsNoTracking()
                    .Any(world => world.Id == subjectCharacterWorldId))
            {
                errorMessage = "世界知识作用域不存在。";
                return AiWorldKnowledgeOperationStatus.CharacterWorldNotFound;
            }

            IQueryable<AiWorldKnowledge> query = dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Where(item =>
                    item.OwnerAiAccountId == ownerAiAccountId
                    && item.SubjectCharacterWorldId
                        == subjectCharacterWorldId
                    && item.Status == AiWorldKnowledgeStatus.Active);

            if (subjectAiAccountId.HasValue)
            {
                query = query.Where(item =>
                    item.SubjectAiAccountId == subjectAiAccountId.Value);
            }

            knowledge = query
                .OrderByDescending(item => item.Salience)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id)
                .Take(maximumCount)
                .ToList()
                .AsReadOnly();
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "世界知识暂时无法读取。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 返回一个账号的世界知识管理列表，可按对象和状态筛选。
    /// 设置页使用此入口，不直接访问 DbContext。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryGetKnowledgeForManagement(
        Guid ownerAiAccountId,
        Guid? subjectAiAccountId,
        AiWorldKnowledgeStatus? status,
        int maximumCount,
        out IReadOnlyList<AiWorldKnowledge> knowledge,
        out string errorMessage)
    {
        knowledge = Array.Empty<AiWorldKnowledge>();

        if (maximumCount is < 1 or > MaximumQueryCount
            || status.HasValue && !Enum.IsDefined(status.Value))
        {
            errorMessage = "世界知识查询条件无效。";
            return AiWorldKnowledgeOperationStatus.InvalidLimit;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            if (!AccountExists(dbContext, ownerAiAccountId))
            {
                errorMessage = "世界知识所有者账号不存在。";
                return AiWorldKnowledgeOperationStatus.AccountNotFound;
            }

            IQueryable<AiWorldKnowledge> query =
                dbContext.AiWorldKnowledge
                    .AsNoTracking()
                    .Where(item =>
                        item.OwnerAiAccountId == ownerAiAccountId);
            if (subjectAiAccountId.HasValue)
            {
                query = query.Where(item =>
                    item.SubjectAiAccountId == subjectAiAccountId);
            }
            if (status.HasValue)
            {
                query = query.Where(item =>
                    item.Status == status.Value);
            }

            knowledge = query
                .OrderBy(item => item.Status)
                .ThenByDescending(item => item.IsUserLocked)
                .ThenByDescending(item => item.Salience)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id)
                .Take(maximumCount)
                .ToList()
                .AsReadOnly();
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "世界知识暂时无法读取。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 返回指定账号名下各条世界知识的来源数量，供管理列表展示来源覆盖度。
    /// 调用方只能查询已经属于该账号的知识，避免跨账号读取计数。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryGetEvidenceCounts(
        Guid ownerAiAccountId,
        IReadOnlyCollection<Guid> knowledgeIds,
        out IReadOnlyDictionary<Guid, int> evidenceCounts,
        out string errorMessage)
    {
        evidenceCounts = new Dictionary<Guid, int>();

        if (knowledgeIds.Count > MaximumQueryCount)
        {
            errorMessage = "单次最多只能统计 100 条世界知识的来源。";
            return AiWorldKnowledgeOperationStatus.InvalidLimit;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            if (!AccountExists(dbContext, ownerAiAccountId))
            {
                errorMessage = "世界知识所有者账号不存在。";
                return AiWorldKnowledgeOperationStatus.AccountNotFound;
            }

            List<Guid> ownedKnowledgeIds = dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Where(item =>
                    item.OwnerAiAccountId == ownerAiAccountId
                    && knowledgeIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToList();
            Dictionary<Guid, int> counts = dbContext
                .AiWorldKnowledgeEvidence
                .AsNoTracking()
                .Where(item =>
                    ownedKnowledgeIds.Contains(item.AiWorldKnowledgeId))
                .GroupBy(item => item.AiWorldKnowledgeId)
                .ToDictionary(group => group.Key, group => group.Count());

            evidenceCounts = ownedKnowledgeIds.ToDictionary(
                id => id,
                id => counts.GetValueOrDefault(id));
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "世界知识来源数量暂时无法读取。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 返回一条属于指定账号的世界知识及其真实来源消息。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryGetEvidenceDetails(
        Guid ownerAiAccountId,
        Guid knowledgeId,
        out IReadOnlyList<AiWorldKnowledgeEvidenceDetails> details,
        out string errorMessage)
    {
        details = Array.Empty<AiWorldKnowledgeEvidenceDetails>();

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            bool knowledgeExists = dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Any(item =>
                    item.Id == knowledgeId
                    && item.OwnerAiAccountId == ownerAiAccountId);
            if (!knowledgeExists)
            {
                errorMessage = "世界知识不存在。";
                return AiWorldKnowledgeOperationStatus.KnowledgeNotFound;
            }

            List<AiWorldKnowledgeEvidence> evidence =
                dbContext.AiWorldKnowledgeEvidence
                    .AsNoTracking()
                    .Where(item =>
                        item.AiWorldKnowledgeId == knowledgeId)
                    .OrderBy(item => item.ObservedAt)
                    .ThenBy(item => item.Id)
                    .ToList();
            List<Guid> privateMessageIds = evidence
                .Where(item => item.SourcePrivateMessageId.HasValue)
                .Select(item => item.SourcePrivateMessageId!.Value)
                .ToList();
            List<Guid> groupMessageIds = evidence
                .Where(item => item.SourceGroupMessageId.HasValue)
                .Select(item => item.SourceGroupMessageId!.Value)
                .ToList();
            Dictionary<Guid, PrivateMessage> privateMessages =
                dbContext.PrivateMessages
                    .AsNoTracking()
                    .Where(item => privateMessageIds.Contains(item.Id))
                    .ToDictionary(item => item.Id);
            Dictionary<Guid, GroupMessage> groupMessages =
                dbContext.GroupMessages
                    .AsNoTracking()
                    .Where(item => groupMessageIds.Contains(item.Id))
                    .ToDictionary(item => item.Id);
            List<Guid> groupChatIds = groupMessages.Values
                .Select(item => item.GroupChatId)
                .Distinct()
                .ToList();
            Dictionary<Guid, string> groupNames = dbContext.GroupChats
                .AsNoTracking()
                .Where(item => groupChatIds.Contains(item.Id))
                .ToDictionary(item => item.Id, item => item.Name);

            details = evidence
                .Select(item => ToEvidenceDetails(
                    item,
                    privateMessages,
                    groupMessages,
                    groupNames))
                .Where(item => item is not null)
                .Cast<AiWorldKnowledgeEvidenceDetails>()
                .ToList()
                .AsReadOnly();
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "世界知识来源暂时无法读取。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 用户修订并可选择确认一条世界知识。确认冲突候选时，
    /// 原有效版本会保留为 Superseded。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryUpdateByUser(
        Guid ownerAiAccountId,
        Guid knowledgeId,
        AiWorldKnowledgeUserUpdateData data,
        out AiWorldKnowledge? knowledge,
        out string errorMessage)
    {
        knowledge = null;
        if (!TryValidateUserUpdate(
                data,
                out string summary,
                out errorMessage))
        {
            return AiWorldKnowledgeOperationStatus.InvalidSummary;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            knowledge = dbContext.AiWorldKnowledge.SingleOrDefault(item =>
                item.Id == knowledgeId
                && item.OwnerAiAccountId == ownerAiAccountId);
            if (knowledge is null)
            {
                errorMessage = "世界知识不存在。";
                return AiWorldKnowledgeOperationStatus.KnowledgeNotFound;
            }
            if (knowledge.Status is AiWorldKnowledgeStatus.Archived
                or AiWorldKnowledgeStatus.Superseded)
            {
                errorMessage = "已归档或已替代的知识不能直接修改。";
                return AiWorldKnowledgeOperationStatus.InvalidStatus;
            }

            DateTime now = DateTime.Now;
            if (knowledge.Status
                    == AiWorldKnowledgeStatus.ConflictCandidate
                && data.IsConfirmed)
            {
                using var transaction =
                    dbContext.Database.BeginTransaction();
                AiWorldKnowledge? current =
                    FindActiveKnowledge(
                        dbContext,
                        knowledge.OwnerAiAccountId,
                        knowledge.SubjectCharacterWorldId,
                        knowledge.SubjectAiAccountId,
                        knowledge.KnowledgeKey);
                if (current is not null)
                {
                    current.MarkAsSuperseded(now);
                    dbContext.SaveChanges();
                }

                knowledge.ReviseByUser(
                    summary,
                    data.FactNature,
                    data.Mutability,
                    data.Salience,
                    data.IsUserLocked,
                    now);
                knowledge.ConfirmByUser(data.IsUserLocked, now);
                dbContext.SaveChanges();
                transaction.Commit();
                errorMessage = string.Empty;
                return AiWorldKnowledgeOperationStatus.Success;
            }
            else if (knowledge.Status
                    == AiWorldKnowledgeStatus.ConflictCandidate)
            {
                knowledge.ReviseConflictCandidate(
                    summary,
                    data.FactNature,
                    data.Mutability,
                    data.Salience,
                    now);
            }
            else
            {
                knowledge.ReviseByUser(
                    summary,
                    data.FactNature,
                    data.Mutability,
                    data.Salience,
                    data.IsUserLocked,
                    now);
            }

            dbContext.SaveChanges();
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }
        catch (DbUpdateException)
        {
            errorMessage = "世界知识暂时无法更新。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "世界知识暂时无法更新。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 用户锁定或解除锁定一条有效世界知识。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TrySetUserLock(
        Guid knowledgeId,
        bool isUserLocked,
        out AiWorldKnowledge? knowledge,
        out string errorMessage)
    {
        return TryChangeUserManagedKnowledge(
            knowledgeId,
            archive: false,
            isUserLocked,
            out knowledge,
            out errorMessage);
    }

    /// <summary>
    /// 用户锁定属于指定账号的一条知识，防止跨账号管理。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TrySetUserLock(
        Guid ownerAiAccountId,
        Guid knowledgeId,
        bool isUserLocked,
        out AiWorldKnowledge? knowledge,
        out string errorMessage)
    {
        return TryChangeUserManagedKnowledge(
            knowledgeId,
            archive: false,
            isUserLocked,
            out knowledge,
            out errorMessage,
            ownerAiAccountId);
    }

    /// <summary>
    /// 用户归档一条世界知识，保留其内容和全部来源证据。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryArchiveByUser(
        Guid knowledgeId,
        out AiWorldKnowledge? knowledge,
        out string errorMessage)
    {
        return TryChangeUserManagedKnowledge(
            knowledgeId,
            archive: true,
            isUserLocked: false,
            out knowledge,
            out errorMessage);
    }

    /// <summary>
    /// 用户归档属于指定账号的一条知识，防止跨账号管理。
    /// </summary>
    public AiWorldKnowledgeOperationStatus TryArchiveByUser(
        Guid ownerAiAccountId,
        Guid knowledgeId,
        out AiWorldKnowledge? knowledge,
        out string errorMessage)
    {
        return TryChangeUserManagedKnowledge(
            knowledgeId,
            archive: true,
            isUserLocked: false,
            out knowledge,
            out errorMessage,
            ownerAiAccountId);
    }

    private AiWorldKnowledgeOperationStatus TryChangeUserManagedKnowledge(
        Guid knowledgeId,
        bool archive,
        bool isUserLocked,
        out AiWorldKnowledge? knowledge,
        out string errorMessage,
        Guid? ownerAiAccountId = null)
    {
        knowledge = null;

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            knowledge = dbContext.AiWorldKnowledge.SingleOrDefault(item =>
                item.Id == knowledgeId
                && (!ownerAiAccountId.HasValue
                    || item.OwnerAiAccountId
                        == ownerAiAccountId.Value));

            if (knowledge is null)
            {
                errorMessage = "世界知识不存在。";
                return AiWorldKnowledgeOperationStatus.KnowledgeNotFound;
            }

            if (archive)
            {
                knowledge.ArchiveByUser(DateTime.Now);
            }
            else
            {
                knowledge.SetUserLock(isUserLocked, DateTime.Now);
            }

            dbContext.SaveChanges();
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }
        catch (DbUpdateException)
        {
            errorMessage = "世界知识暂时无法更新。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "世界知识暂时无法更新。";
            return AiWorldKnowledgeOperationStatus.PersistenceFailed;
        }
    }

    private static AiWorldKnowledgeOperationStatus ValidateInput(
        AiWorldKnowledgeWriteData data,
        string evidenceSummary,
        out string normalizedKnowledgeKey,
        out string normalizedSummary,
        out string normalizedEvidenceSummary,
        out string errorMessage)
    {
        normalizedKnowledgeKey = string.Empty;
        normalizedSummary = string.Empty;
        normalizedEvidenceSummary = string.Empty;

        if (data is null)
        {
            errorMessage = "世界知识内容不能为空。";
            return AiWorldKnowledgeOperationStatus.InvalidSummary;
        }

        if (string.IsNullOrWhiteSpace(data.KnowledgeKey))
        {
            errorMessage = "世界知识事实键不能为空。";
            return AiWorldKnowledgeOperationStatus.InvalidKnowledgeKey;
        }

        normalizedKnowledgeKey = data.KnowledgeKey
            .Trim()
            .ToLowerInvariant();
        if (normalizedKnowledgeKey.Length
            > AiWorldKnowledge.KnowledgeKeyMaxLength)
        {
            errorMessage =
                $"世界知识事实键不能超过 {AiWorldKnowledge.KnowledgeKeyMaxLength} 个字符。";
            return AiWorldKnowledgeOperationStatus.InvalidKnowledgeKey;
        }

        if (string.IsNullOrWhiteSpace(data.Summary))
        {
            errorMessage = "世界知识摘要不能为空。";
            return AiWorldKnowledgeOperationStatus.InvalidSummary;
        }

        normalizedSummary = data.Summary.Trim();
        if (normalizedSummary.Length > AiWorldKnowledge.SummaryMaxLength)
        {
            errorMessage =
                $"世界知识摘要不能超过 {AiWorldKnowledge.SummaryMaxLength} 个字符。";
            return AiWorldKnowledgeOperationStatus.InvalidSummary;
        }

        if (string.IsNullOrWhiteSpace(evidenceSummary))
        {
            errorMessage = "世界知识证据摘要不能为空。";
            return AiWorldKnowledgeOperationStatus.InvalidSummary;
        }

        normalizedEvidenceSummary = evidenceSummary.Trim();
        if (normalizedEvidenceSummary.Length
            > AiWorldKnowledgeEvidence.EvidenceSummaryMaxLength)
        {
            errorMessage =
                $"证据摘要不能超过 {AiWorldKnowledgeEvidence.EvidenceSummaryMaxLength} 个字符。";
            return AiWorldKnowledgeOperationStatus.InvalidSummary;
        }

        if (!Enum.IsDefined(data.FactNature)
            || !Enum.IsDefined(data.Mutability)
            || !Enum.IsDefined(data.TrustLevel))
        {
            errorMessage = "世界知识分类无效。";
            return AiWorldKnowledgeOperationStatus.InvalidClassification;
        }

        if (data.Salience is < AiWorldKnowledge.MinimumSalience
            or > AiWorldKnowledge.MaximumSalience)
        {
            errorMessage = "世界知识显著度必须在 1 到 100 之间。";
            return AiWorldKnowledgeOperationStatus.InvalidSalience;
        }

        errorMessage = string.Empty;
        return AiWorldKnowledgeOperationStatus.Success;
    }

    private static AiWorldKnowledgeOperationStatus ValidateKnowledgeScope(
        VocaChatDbContext dbContext,
        AiWorldKnowledgeWriteData data,
        out string errorMessage)
    {
        AiAccount? owner = dbContext.AiAccounts
            .AsNoTracking()
            .SingleOrDefault(account =>
                account.Id == data.OwnerAiAccountId);

        if (owner is null)
        {
            errorMessage = "世界知识所有者账号不存在。";
            return AiWorldKnowledgeOperationStatus.AccountNotFound;
        }

        if (!dbContext.CharacterWorlds
                .AsNoTracking()
                .Any(world =>
                    world.Id == data.SubjectCharacterWorldId))
        {
            errorMessage = "世界知识作用域不存在。";
            return AiWorldKnowledgeOperationStatus.CharacterWorldNotFound;
        }

        if (owner.CharacterWorldId == data.SubjectCharacterWorldId)
        {
            errorMessage = "当前账号自身世界的事实应保存为个人记忆，而不是其他世界知识。";
            return AiWorldKnowledgeOperationStatus.InvalidSubject;
        }

        if (!data.SubjectAiAccountId.HasValue)
        {
            errorMessage = string.Empty;
            return AiWorldKnowledgeOperationStatus.Success;
        }

        if (data.SubjectAiAccountId.Value == data.OwnerAiAccountId)
        {
            errorMessage = "世界知识所有者不能同时作为知识对象。";
            return AiWorldKnowledgeOperationStatus.InvalidSubject;
        }

        AiAccount? subject = dbContext.AiAccounts
            .AsNoTracking()
            .SingleOrDefault(account =>
                account.Id == data.SubjectAiAccountId.Value);

        if (subject is null)
        {
            errorMessage = "世界知识对象账号不存在。";
            return AiWorldKnowledgeOperationStatus.AccountNotFound;
        }

        if (subject.CharacterWorldId != data.SubjectCharacterWorldId)
        {
            errorMessage = "世界知识对象与指定世界作用域不一致。";
            return AiWorldKnowledgeOperationStatus.InvalidSubject;
        }

        errorMessage = string.Empty;
        return AiWorldKnowledgeOperationStatus.Success;
    }

    private static AiWorldKnowledge? FindActiveKnowledge(
        VocaChatDbContext dbContext,
        Guid ownerAiAccountId,
        Guid subjectCharacterWorldId,
        Guid? subjectAiAccountId,
        string knowledgeKey)
    {
        return dbContext.AiWorldKnowledge.SingleOrDefault(item =>
            item.OwnerAiAccountId == ownerAiAccountId
            && item.SubjectCharacterWorldId == subjectCharacterWorldId
            && item.SubjectAiAccountId == subjectAiAccountId
            && item.KnowledgeKey == knowledgeKey
            && item.Status == AiWorldKnowledgeStatus.Active);
    }

    private static AiWorldKnowledge? FindEquivalentActiveKnowledge(
        VocaChatDbContext dbContext,
        Guid ownerAiAccountId,
        Guid subjectCharacterWorldId,
        Guid? subjectAiAccountId,
        string summary)
    {
        return dbContext.AiWorldKnowledge
            .Where(item =>
                item.OwnerAiAccountId == ownerAiAccountId
                && item.SubjectCharacterWorldId == subjectCharacterWorldId
                && item.SubjectAiAccountId == subjectAiAccountId
                && item.Status == AiWorldKnowledgeStatus.Active)
            .AsEnumerable()
            .FirstOrDefault(item =>
                AreEquivalentSummaries(item.Summary, summary));
    }

    private static AiWorldKnowledge?
        FindEquivalentConflictCandidate(
            VocaChatDbContext dbContext,
            Guid ownerAiAccountId,
            Guid subjectCharacterWorldId,
            Guid? subjectAiAccountId,
            string summary)
    {
        return dbContext.AiWorldKnowledge
            .Where(item =>
                item.OwnerAiAccountId == ownerAiAccountId
                && item.SubjectCharacterWorldId == subjectCharacterWorldId
                && item.SubjectAiAccountId == subjectAiAccountId
                && item.Status
                    == AiWorldKnowledgeStatus.ConflictCandidate)
            .AsEnumerable()
            .FirstOrDefault(item =>
                AreEquivalentSummaries(item.Summary, summary));
    }

    private static AiWorldKnowledge CreateConflictCandidate(
        VocaChatDbContext dbContext,
        AiWorldKnowledgeWriteData data,
        string knowledgeKey,
        string summary,
        DateTime observedAt)
    {
        AiWorldKnowledge candidate = new(
            data.OwnerAiAccountId,
            data.SubjectCharacterWorldId,
            data.SubjectAiAccountId,
            knowledgeKey,
            summary,
            data.FactNature,
            data.Mutability,
            AiWorldKnowledgeTrustLevel.Unverified,
            data.Salience,
            isUserLocked: false,
            observedAt);
        candidate.MarkAsConflictCandidate(observedAt);
        dbContext.AiWorldKnowledge.Add(candidate);
        return candidate;
    }

    private static bool ShouldCreateConflictCandidate(
        AiWorldKnowledge current,
        AiWorldKnowledgeWriteData incoming)
    {
        return current.IsUserLocked
            || (current.FactNature
                    == AiWorldKnowledgeFactNature.ObjectiveStatement
                && current.Mutability
                    == AiWorldKnowledgeMutability.Constant
                && incoming.FactNature
                    == AiWorldKnowledgeFactNature.ObjectiveStatement
                && incoming.Mutability
                    == AiWorldKnowledgeMutability.Constant);
    }

    private static bool ShouldSupersede(
        AiWorldKnowledge current,
        AiWorldKnowledgeWriteData incoming)
    {
        if (current.IsUserLocked)
        {
            return false;
        }

        return current.FactNature
                == AiWorldKnowledgeFactNature.SubjectiveView
            || incoming.FactNature
                == AiWorldKnowledgeFactNature.SubjectiveView
            || (current.FactNature
                    == AiWorldKnowledgeFactNature.ObjectiveStatement
                && incoming.FactNature
                    == AiWorldKnowledgeFactNature.ObjectiveStatement
                && (current.Mutability
                        != AiWorldKnowledgeMutability.Constant
                    || incoming.Mutability
                        != AiWorldKnowledgeMutability.Constant));
    }

    private static bool AreEquivalentSummaries(
        string first,
        string second)
    {
        string normalizedFirst = NormalizeSummary(first);
        string normalizedSecond = NormalizeSummary(second);
        if (string.Equals(
                normalizedFirst,
                normalizedSecond,
                StringComparison.Ordinal))
        {
            return true;
        }

        int shorterLength = Math.Min(
            normalizedFirst.Length,
            normalizedSecond.Length);
        int longerLength = Math.Max(
            normalizedFirst.Length,
            normalizedSecond.Length);
        if (shorterLength >= 8
            && longerLength > 0
            && (normalizedFirst.Contains(
                    normalizedSecond,
                    StringComparison.Ordinal)
                || normalizedSecond.Contains(
                    normalizedFirst,
                    StringComparison.Ordinal))
            && (double)shorterLength / longerLength >= 0.65)
        {
            return true;
        }

        HashSet<string> firstFragments = CreateBigrams(normalizedFirst);
        HashSet<string> secondFragments = CreateBigrams(normalizedSecond);
        if (firstFragments.Count == 0 || secondFragments.Count == 0)
        {
            return false;
        }

        int intersection = firstFragments.Intersect(secondFragments).Count();
        int union = firstFragments.Union(secondFragments).Count();
        return union > 0 && (double)intersection / union >= 0.55;
    }

    private static string NormalizeSummary(string value)
    {
        string withoutAttribution = value
            .Replace("提到", string.Empty, StringComparison.Ordinal)
            .Replace("表示", string.Empty, StringComparison.Ordinal)
            .Replace("称", string.Empty, StringComparison.Ordinal)
            .Replace("是一所高中", "是学校", StringComparison.Ordinal)
            .Replace("是个学校", "是学校", StringComparison.Ordinal)
            .Replace("是一所学校", "是学校", StringComparison.Ordinal)
            .Replace("沙漠侵蚀", "沙漠化", StringComparison.Ordinal);
        return new string(withoutAttribution
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static HashSet<string> CreateBigrams(string value)
    {
        HashSet<string> fragments = new(StringComparer.Ordinal);
        if (value.Length < 2)
        {
            if (value.Length == 1)
            {
                fragments.Add(value);
            }

            return fragments;
        }

        for (int index = 0; index < value.Length - 1; index++)
        {
            fragments.Add(value.Substring(index, 2));
        }

        return fragments;
    }

    private static void PromoteTrustFromIndependentSources(
        VocaChatDbContext dbContext,
        AiWorldKnowledge knowledge,
        ValidatedConversationEvidence newSource)
    {
        if (knowledge.TrustLevel
                == AiWorldKnowledgeTrustLevel.UserConfirmed
            || knowledge.Status
                == AiWorldKnowledgeStatus.ConflictCandidate)
        {
            return;
        }

        HashSet<string> sources = dbContext.AiWorldKnowledgeEvidence
            .AsNoTracking()
            .Where(item => item.AiWorldKnowledgeId == knowledge.Id)
            .Select(item => new
            {
                item.SourceType,
                item.SourceAiAccountId
            })
            .AsEnumerable()
            .Select(item => CreateSourceIdentity(
                item.SourceType,
                item.SourceAiAccountId))
            .ToHashSet(StringComparer.Ordinal);
        sources.Add(CreateSourceIdentity(
            newSource.SourceType,
            newSource.SourceAiAccountId));

        if (sources.Count >= 2)
        {
            knowledge.PromoteTrust(
                AiWorldKnowledgeTrustLevel.Corroborated,
                newSource.ObservedAt);
        }
    }

    private static string CreateSourceIdentity(
        MessageSenderType sourceType,
        Guid? sourceAiAccountId)
    {
        return sourceType == MessageSenderType.User
            ? "user"
            : $"ai:{sourceAiAccountId:N}";
    }

    private static bool AccountExists(
        VocaChatDbContext dbContext,
        Guid aiAccountId)
    {
        return dbContext.AiAccounts
            .AsNoTracking()
            .Any(account => account.Id == aiAccountId);
    }

    private IReadOnlyList<GroupMessageAudience>
        ReadGroupMessageAudience(Guid groupMessageId)
    {
        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            return dbContext.GroupMessageAudience
                .AsNoTracking()
                .Where(item => item.GroupMessageId == groupMessageId)
                .OrderBy(item => item.AiAccountId)
                .ToList()
                .AsReadOnly();
        }
        catch (SqliteException)
        {
            return Array.Empty<GroupMessageAudience>();
        }
    }

    private static AiWorldKnowledgeEvidenceDetails? ToEvidenceDetails(
        AiWorldKnowledgeEvidence evidence,
        IReadOnlyDictionary<Guid, PrivateMessage> privateMessages,
        IReadOnlyDictionary<Guid, GroupMessage> groupMessages,
        IReadOnlyDictionary<Guid, string> groupNames)
    {
        if (evidence.SourcePrivateMessageId is Guid privateMessageId
            && privateMessages.TryGetValue(
                privateMessageId,
                out PrivateMessage? privateMessage))
        {
            return new AiWorldKnowledgeEvidenceDetails(
                evidence.Id,
                evidence.SourceType,
                evidence.SourceAiAccountId,
                privateMessage.SenderDisplayName,
                "PrivateChat",
                privateMessage.PrivateChatId,
                "私聊",
                privateMessage.Id,
                privateMessage.Content,
                privateMessage.SentAt,
                evidence.EvidenceSummary);
        }

        if (evidence.SourceGroupMessageId is Guid groupMessageId
            && groupMessages.TryGetValue(
                groupMessageId,
                out GroupMessage? groupMessage))
        {
            return new AiWorldKnowledgeEvidenceDetails(
                evidence.Id,
                evidence.SourceType,
                evidence.SourceAiAccountId,
                groupMessage.SenderDisplayName,
                "GroupChat",
                groupMessage.GroupChatId,
                groupNames.GetValueOrDefault(
                    groupMessage.GroupChatId,
                    "群聊"),
                groupMessage.Id,
                groupMessage.Content,
                groupMessage.SentAt,
                evidence.EvidenceSummary);
        }

        return null;
    }

    private static bool TryValidateUserUpdate(
        AiWorldKnowledgeUserUpdateData data,
        out string summary,
        out string errorMessage)
    {
        summary = string.Empty;
        if (data is null || string.IsNullOrWhiteSpace(data.Summary))
        {
            errorMessage = "世界知识摘要不能为空。";
            return false;
        }

        summary = data.Summary.Trim();
        if (summary.Length > AiWorldKnowledge.SummaryMaxLength)
        {
            errorMessage =
                $"世界知识摘要不能超过 {AiWorldKnowledge.SummaryMaxLength} 个字符。";
            return false;
        }

        if (!Enum.IsDefined(data.FactNature)
            || !Enum.IsDefined(data.Mutability))
        {
            errorMessage = "世界知识分类无效。";
            return false;
        }

        if (data.Salience is < AiWorldKnowledge.MinimumSalience
            or > AiWorldKnowledge.MaximumSalience)
        {
            errorMessage = "世界知识显著度必须在 1 到 100 之间。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static AiWorldKnowledgeOperationStatus MapSourceFailure(
        AiConversationEvidenceValidationStatus status)
    {
        return status switch
        {
            AiConversationEvidenceValidationStatus.SourceNotFound =>
                AiWorldKnowledgeOperationStatus.SourceNotFound,
            AiConversationEvidenceValidationStatus.SourceNotVisible =>
                AiWorldKnowledgeOperationStatus.SourceNotVisible,
            AiConversationEvidenceValidationStatus.SelfAuthoredSource =>
                AiWorldKnowledgeOperationStatus.SelfAuthoredSource,
            _ => AiWorldKnowledgeOperationStatus.InvalidSource
        };
    }
}

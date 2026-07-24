using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责账号级平行世界元认知和 AI 账号之间方向性世界认知的持久化。
/// </summary>
public sealed class AiWorldAwarenessService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiWorldAwarenessService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 返回账号级平行世界认知；没有数据库记录时返回 Unaware。
    /// </summary>
    public AiWorldAwarenessOperationStatus TryGetParallelWorldAwareness(
        Guid aiAccountId,
        out AiParallelWorldAwarenessState state,
        out AiParallelWorldAwareness? awareness,
        out string errorMessage)
    {
        state = AiParallelWorldAwarenessState.Unaware;
        awareness = null;

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!AccountExists(dbContext, aiAccountId))
            {
                errorMessage = "AI 账号不存在。";
                return AiWorldAwarenessOperationStatus.AccountNotFound;
            }

            awareness = dbContext.AiParallelWorldAwareness
                .AsNoTracking()
                .SingleOrDefault(item => item.AiAccountId == aiAccountId);
            state = awareness?.State
                ?? AiParallelWorldAwarenessState.Unaware;
            errorMessage = string.Empty;
            return AiWorldAwarenessOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "平行世界认知暂时无法读取。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 返回 Observer 对 Subject 的方向性认知；没有数据库记录时返回 AssumedSharedWorld。
    /// </summary>
    public AiWorldAwarenessOperationStatus TryGetWorldAwareness(
        Guid observerAiAccountId,
        Guid subjectAiAccountId,
        out AiWorldAwarenessState state,
        out AiWorldAwareness? awareness,
        out string errorMessage)
    {
        state = AiWorldAwarenessState.AssumedSharedWorld;
        awareness = null;

        if (observerAiAccountId == subjectAiAccountId)
        {
            errorMessage = "世界认知的观察者和对象不能是同一个账号。";
            return AiWorldAwarenessOperationStatus.SameAccountNotAllowed;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!AccountsExist(
                    dbContext,
                    observerAiAccountId,
                    subjectAiAccountId))
            {
                errorMessage = "观察者或对象 AI 账号不存在。";
                return AiWorldAwarenessOperationStatus.AccountNotFound;
            }

            awareness = dbContext.AiWorldAwareness
                .AsNoTracking()
                .SingleOrDefault(item =>
                    item.ObserverAiAccountId == observerAiAccountId
                    && item.SubjectAiAccountId == subjectAiAccountId);
            state = awareness?.State
                ?? AiWorldAwarenessState.AssumedSharedWorld;
            errorMessage = string.Empty;
            return AiWorldAwarenessOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "方向性世界认知暂时无法读取。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 根据当前有效知识、独立主题和独立会话实时派生 Observer
    /// 对 Subject 所处世界的熟悉度，不保存可被模型任意修改的分数。
    /// </summary>
    public AiWorldAwarenessOperationStatus TryGetFamiliarity(
        Guid observerAiAccountId,
        Guid subjectAiAccountId,
        out AiWorldFamiliarity familiarity,
        out string errorMessage)
    {
        familiarity = new AiWorldFamiliarity(
            AiWorldFamiliarityLevel.Unfamiliar,
            ActiveKnowledgeCount: 0,
            DistinctTopicCount: 0,
            EvidenceCount: 0,
            DistinctConversationCount: 0);

        if (observerAiAccountId == subjectAiAccountId)
        {
            errorMessage = "世界认知的观察者和对象不能是同一个账号。";
            return AiWorldAwarenessOperationStatus.SameAccountNotAllowed;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            if (!AccountsExist(
                    dbContext,
                    observerAiAccountId,
                    subjectAiAccountId))
            {
                errorMessage = "观察者或对象 AI 账号不存在。";
                return AiWorldAwarenessOperationStatus.AccountNotFound;
            }

            List<AiWorldKnowledge> knowledge = dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Where(item =>
                    item.OwnerAiAccountId == observerAiAccountId
                    && item.SubjectAiAccountId == subjectAiAccountId
                    && item.Status == AiWorldKnowledgeStatus.Active)
                .ToList();
            List<Guid> knowledgeIds = knowledge
                .Select(item => item.Id)
                .ToList();
            List<AiWorldKnowledgeEvidence> evidence =
                dbContext.AiWorldKnowledgeEvidence
                    .AsNoTracking()
                    .Where(item =>
                        knowledgeIds.Contains(item.AiWorldKnowledgeId))
                    .ToList();
            int distinctConversationCount =
                CountDistinctConversations(dbContext, evidence);
            int distinctTopicCount = knowledge
                .Select(item => item.KnowledgeKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            AiWorldFamiliarityLevel level = ResolveFamiliarityLevel(
                knowledge.Count,
                distinctTopicCount,
                distinctConversationCount);

            familiarity = new AiWorldFamiliarity(
                level,
                knowledge.Count,
                distinctTopicCount,
                evidence.Count,
                distinctConversationCount);
            errorMessage = string.Empty;
            return AiWorldAwarenessOperationStatus.Success;
        }
        catch (SqliteException)
        {
            errorMessage = "世界熟悉度暂时无法读取。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 从一条对当前账号真实可见的消息提高平行世界元认知。
    /// 自动流程不能降低状态，也不能修改用户锁定记录。
    /// </summary>
    public AiWorldAwarenessOperationStatus TryRecordParallelWorldAwareness(
        Guid aiAccountId,
        AiParallelWorldAwarenessState state,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        out AiParallelWorldAwareness? awareness,
        out string errorMessage)
    {
        awareness = null;

        if (!Enum.IsDefined(state)
            || state == AiParallelWorldAwarenessState.Unaware)
        {
            errorMessage = "消息只能将平行世界认知推进到已获知或已接受。";
            return AiWorldAwarenessOperationStatus.InvalidState;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!AccountExists(dbContext, aiAccountId))
            {
                errorMessage = "AI 账号不存在。";
                return AiWorldAwarenessOperationStatus.AccountNotFound;
            }

            if (!AiConversationEvidenceValidator.TryValidate(
                    dbContext,
                    aiAccountId,
                    sourcePrivateMessageId,
                    sourceGroupMessageId,
                    out AiConversationEvidenceValidationStatus sourceStatus,
                    out ValidatedConversationEvidence? evidence,
                    out errorMessage))
            {
                return MapSourceFailure(sourceStatus);
            }

            awareness = dbContext.AiParallelWorldAwareness
                .SingleOrDefault(item => item.AiAccountId == aiAccountId);

            if (awareness?.IsUserLocked == true)
            {
                errorMessage = "该账号的平行世界认知已由用户锁定。";
                return AiWorldAwarenessOperationStatus.UserLocked;
            }

            if (awareness is not null && state < awareness.State)
            {
                errorMessage = "自动流程不能降低已有平行世界认知。";
                return AiWorldAwarenessOperationStatus.StateRegressionNotAllowed;
            }

            DateTime now = evidence!.ObservedAt;

            if (awareness is null)
            {
                awareness = new AiParallelWorldAwareness(
                    aiAccountId,
                    state,
                    evidence.SourcePrivateMessageId,
                    evidence.SourceGroupMessageId,
                    isUserLocked: false,
                    now);
                dbContext.AiParallelWorldAwareness.Add(awareness);
            }
            else
            {
                awareness.Update(
                    state,
                    evidence.SourcePrivateMessageId,
                    evidence.SourceGroupMessageId,
                    isUserLocked: false,
                    now);
            }

            dbContext.SaveChanges();
            errorMessage = string.Empty;
            return AiWorldAwarenessOperationStatus.Success;
        }
        catch (DbUpdateException)
        {
            errorMessage = "平行世界认知暂时无法保存。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "平行世界认知暂时无法保存。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 由本地用户直接设置并选择是否锁定账号级平行世界认知。
    /// </summary>
    public AiWorldAwarenessOperationStatus TrySetParallelWorldAwarenessByUser(
        Guid aiAccountId,
        AiParallelWorldAwarenessState state,
        bool isUserLocked,
        out AiParallelWorldAwareness? awareness,
        out string errorMessage)
    {
        awareness = null;

        if (!Enum.IsDefined(state))
        {
            errorMessage = "平行世界认知状态无效。";
            return AiWorldAwarenessOperationStatus.InvalidState;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();

            if (!AccountExists(dbContext, aiAccountId))
            {
                errorMessage = "AI 账号不存在。";
                return AiWorldAwarenessOperationStatus.AccountNotFound;
            }

            awareness = dbContext.AiParallelWorldAwareness
                .SingleOrDefault(item => item.AiAccountId == aiAccountId);
            DateTime now = DateTime.Now;

            if (awareness is null)
            {
                awareness = new AiParallelWorldAwareness(
                    aiAccountId,
                    state,
                    sourcePrivateMessageId: null,
                    sourceGroupMessageId: null,
                    isUserLocked,
                    now);
                dbContext.AiParallelWorldAwareness.Add(awareness);
            }
            else
            {
                awareness.Update(
                    state,
                    sourcePrivateMessageId: null,
                    sourceGroupMessageId: null,
                    isUserLocked,
                    now);
            }

            dbContext.SaveChanges();
            errorMessage = string.Empty;
            return AiWorldAwarenessOperationStatus.Success;
        }
        catch (DbUpdateException)
        {
            errorMessage = "平行世界认知暂时无法保存。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "平行世界认知暂时无法保存。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 从一条真实可见消息推进 Observer 对 Subject 的方向性认知。
    /// </summary>
    public AiWorldAwarenessOperationStatus TryRecordWorldAwareness(
        Guid observerAiAccountId,
        Guid subjectAiAccountId,
        AiWorldAwarenessState state,
        int evidenceCount,
        int distinctConversationCount,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId,
        out AiWorldAwareness? awareness,
        out string errorMessage)
    {
        awareness = null;

        AiWorldAwarenessOperationStatus inputStatus =
            ValidateWorldAwarenessInput(
                observerAiAccountId,
                subjectAiAccountId,
                state,
                evidenceCount,
                distinctConversationCount,
                allowAssumedSharedWorld: false,
                out errorMessage);
        if (inputStatus != AiWorldAwarenessOperationStatus.Success)
        {
            return inputStatus;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiAccount? subject = dbContext.AiAccounts
                .AsNoTracking()
                .SingleOrDefault(account =>
                    account.Id == subjectAiAccountId);

            if (subject is null
                || !AccountExists(dbContext, observerAiAccountId))
            {
                errorMessage = "观察者或对象 AI 账号不存在。";
                return AiWorldAwarenessOperationStatus.AccountNotFound;
            }

            if (!AiConversationEvidenceValidator.TryValidate(
                    dbContext,
                    observerAiAccountId,
                    sourcePrivateMessageId,
                    sourceGroupMessageId,
                    out AiConversationEvidenceValidationStatus sourceStatus,
                    out ValidatedConversationEvidence? evidence,
                    out errorMessage))
            {
                return MapSourceFailure(sourceStatus);
            }

            awareness = dbContext.AiWorldAwareness.SingleOrDefault(item =>
                item.ObserverAiAccountId == observerAiAccountId
                && item.SubjectAiAccountId == subjectAiAccountId);

            if (awareness?.IsUserLocked == true)
            {
                errorMessage = "该方向性世界认知已由用户锁定。";
                return AiWorldAwarenessOperationStatus.UserLocked;
            }

            if (awareness is not null
                && (state < awareness.State
                    || evidenceCount < awareness.EvidenceCount
                    || distinctConversationCount
                        < awareness.DistinctConversationCount))
            {
                errorMessage = "自动流程不能降低已有状态或证据计数。";
                return AiWorldAwarenessOperationStatus.StateRegressionNotAllowed;
            }

            DateTime observedAt = evidence!.ObservedAt;
            DateTime? firstEvidenceAt =
                awareness?.FirstEvidenceAt ?? observedAt;
            DateTime? confirmedAt =
                state == AiWorldAwarenessState.CrossWorldConfirmed
                    ? awareness?.ConfirmedAt ?? observedAt
                    : null;

            if (awareness is null)
            {
                awareness = new AiWorldAwareness(
                    observerAiAccountId,
                    subjectAiAccountId,
                    subject.CharacterWorldId,
                    state,
                    evidenceCount,
                    distinctConversationCount,
                    firstEvidenceAt,
                    observedAt,
                    confirmedAt,
                    evidence.SourcePrivateMessageId,
                    evidence.SourceGroupMessageId,
                    isUserLocked: false,
                    observedAt);
                dbContext.AiWorldAwareness.Add(awareness);
            }
            else
            {
                awareness.Update(
                    subject.CharacterWorldId,
                    state,
                    evidenceCount,
                    distinctConversationCount,
                    firstEvidenceAt,
                    observedAt,
                    confirmedAt,
                    evidence.SourcePrivateMessageId,
                    evidence.SourceGroupMessageId,
                    isUserLocked: false,
                    observedAt);
            }

            dbContext.SaveChanges();
            errorMessage = string.Empty;
            return AiWorldAwarenessOperationStatus.Success;
        }
        catch (DbUpdateException)
        {
            errorMessage = "方向性世界认知暂时无法保存。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "方向性世界认知暂时无法保存。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
    }

    /// <summary>
    /// 由本地用户直接设置并选择是否锁定 A 对 B 的世界认知。
    /// </summary>
    public AiWorldAwarenessOperationStatus TrySetWorldAwarenessByUser(
        Guid observerAiAccountId,
        Guid subjectAiAccountId,
        AiWorldAwarenessState state,
        bool isUserLocked,
        out AiWorldAwareness? awareness,
        out string errorMessage)
    {
        awareness = null;
        AiWorldAwarenessOperationStatus inputStatus =
            ValidateWorldAwarenessInput(
                observerAiAccountId,
                subjectAiAccountId,
                state,
                evidenceCount: 0,
                distinctConversationCount: 0,
                allowAssumedSharedWorld: true,
                out errorMessage);
        if (inputStatus != AiWorldAwarenessOperationStatus.Success)
        {
            return inputStatus;
        }

        try
        {
            using VocaChatDbContext dbContext =
                _dbContextFactory.CreateDbContext();
            AiAccount? subject = dbContext.AiAccounts
                .AsNoTracking()
                .SingleOrDefault(account =>
                    account.Id == subjectAiAccountId);

            if (subject is null
                || !AccountExists(dbContext, observerAiAccountId))
            {
                errorMessage = "观察者或对象 AI 账号不存在。";
                return AiWorldAwarenessOperationStatus.AccountNotFound;
            }

            awareness = dbContext.AiWorldAwareness.SingleOrDefault(item =>
                item.ObserverAiAccountId == observerAiAccountId
                && item.SubjectAiAccountId == subjectAiAccountId);
            DateTime now = DateTime.Now;
            int evidenceCount = awareness?.EvidenceCount ?? 0;
            int conversationCount =
                awareness?.DistinctConversationCount ?? 0;
            DateTime? confirmedAt =
                state == AiWorldAwarenessState.CrossWorldConfirmed
                    ? awareness?.ConfirmedAt ?? now
                    : null;

            if (awareness is null)
            {
                awareness = new AiWorldAwareness(
                    observerAiAccountId,
                    subjectAiAccountId,
                    subject.CharacterWorldId,
                    state,
                    evidenceCount,
                    conversationCount,
                    firstEvidenceAt: null,
                    lastEvidenceAt: null,
                    confirmedAt,
                    sourcePrivateMessageId: null,
                    sourceGroupMessageId: null,
                    isUserLocked,
                    now);
                dbContext.AiWorldAwareness.Add(awareness);
            }
            else
            {
                awareness.Update(
                    subject.CharacterWorldId,
                    state,
                    evidenceCount,
                    conversationCount,
                    awareness.FirstEvidenceAt,
                    awareness.LastEvidenceAt,
                    confirmedAt,
                    sourcePrivateMessageId: null,
                    sourceGroupMessageId: null,
                    isUserLocked,
                    now);
            }

            dbContext.SaveChanges();
            errorMessage = string.Empty;
            return AiWorldAwarenessOperationStatus.Success;
        }
        catch (DbUpdateException)
        {
            errorMessage = "方向性世界认知暂时无法保存。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
        catch (SqliteException)
        {
            errorMessage = "方向性世界认知暂时无法保存。";
            return AiWorldAwarenessOperationStatus.PersistenceFailed;
        }
    }

    private static int CountDistinctConversations(
        VocaChatDbContext dbContext,
        IReadOnlyList<AiWorldKnowledgeEvidence> evidence)
    {
        HashSet<string> conversationKeys =
            new(StringComparer.Ordinal);
        List<Guid> privateMessageIds = evidence
            .Where(item => item.SourcePrivateMessageId.HasValue)
            .Select(item => item.SourcePrivateMessageId!.Value)
            .Distinct()
            .ToList();
        foreach (PrivateMessage message in dbContext.PrivateMessages
                     .AsNoTracking()
                     .Where(item => privateMessageIds.Contains(item.Id)))
        {
            conversationKeys.Add(
                message.AutonomousPrivateChatSessionId is Guid sessionId
                    ? $"private-session:{sessionId:N}"
                    : $"private-chat:{message.PrivateChatId:N}");
        }

        List<Guid> groupMessageIds = evidence
            .Where(item => item.SourceGroupMessageId.HasValue)
            .Select(item => item.SourceGroupMessageId!.Value)
            .Distinct()
            .ToList();
        foreach (GroupMessage message in dbContext.GroupMessages
                     .AsNoTracking()
                     .Where(item => groupMessageIds.Contains(item.Id)))
        {
            string key = message.AutonomousGroupChatSessionId
                    is Guid sessionId
                ? $"group-session:{sessionId:N}"
                : message.InteractionBatchId is Guid interactionBatchId
                    ? $"group-interaction:{interactionBatchId:N}"
                    : $"group-chat:{message.GroupChatId:N}";
            conversationKeys.Add(key);
        }

        return conversationKeys.Count;
    }

    private static AiWorldFamiliarityLevel ResolveFamiliarityLevel(
        int activeKnowledgeCount,
        int distinctTopicCount,
        int distinctConversationCount)
    {
        if (activeKnowledgeCount == 0)
        {
            return AiWorldFamiliarityLevel.Unfamiliar;
        }

        if (distinctTopicCount >= 6 && distinctConversationCount >= 3)
        {
            return AiWorldFamiliarityLevel.Familiar;
        }

        if (distinctTopicCount >= 3 && distinctConversationCount >= 2)
        {
            return AiWorldFamiliarityLevel.Learning;
        }

        return AiWorldFamiliarityLevel.FirstImpression;
    }

    private static AiWorldAwarenessOperationStatus
        ValidateWorldAwarenessInput(
            Guid observerAiAccountId,
            Guid subjectAiAccountId,
            AiWorldAwarenessState state,
            int evidenceCount,
            int distinctConversationCount,
            bool allowAssumedSharedWorld,
            out string errorMessage)
    {
        if (observerAiAccountId == subjectAiAccountId)
        {
            errorMessage = "世界认知的观察者和对象不能是同一个账号。";
            return AiWorldAwarenessOperationStatus.SameAccountNotAllowed;
        }

        if (!Enum.IsDefined(state)
            || (!allowAssumedSharedWorld
                && state == AiWorldAwarenessState.AssumedSharedWorld))
        {
            errorMessage = "方向性世界认知状态无效。";
            return AiWorldAwarenessOperationStatus.InvalidState;
        }

        if (evidenceCount < 0 || distinctConversationCount < 0)
        {
            errorMessage = "世界认知证据和会话数量不能为负数。";
            return AiWorldAwarenessOperationStatus.InvalidEvidenceCount;
        }

        errorMessage = string.Empty;
        return AiWorldAwarenessOperationStatus.Success;
    }

    private static AiWorldAwarenessOperationStatus MapSourceFailure(
        AiConversationEvidenceValidationStatus status)
    {
        if (status
            == AiConversationEvidenceValidationStatus.SelfAuthoredSource)
        {
            return AiWorldAwarenessOperationStatus.SelfAuthoredSource;
        }

        if (status == AiConversationEvidenceValidationStatus.SourceNotVisible)
        {
            return AiWorldAwarenessOperationStatus.SourceNotVisible;
        }

        if (status == AiConversationEvidenceValidationStatus.SourceNotFound)
        {
            return AiWorldAwarenessOperationStatus.SourceNotFound;
        }

        return AiWorldAwarenessOperationStatus.InvalidSource;
    }

    private static bool AccountExists(
        VocaChatDbContext dbContext,
        Guid accountId)
    {
        return dbContext.AiAccounts
            .AsNoTracking()
            .Any(account => account.Id == accountId);
    }

    private static bool AccountsExist(
        VocaChatDbContext dbContext,
        Guid firstAccountId,
        Guid secondAccountId)
    {
        return dbContext.AiAccounts
            .AsNoTracking()
            .Count(account =>
                account.Id == firstAccountId
                || account.Id == secondAccountId) == 2;
    }
}

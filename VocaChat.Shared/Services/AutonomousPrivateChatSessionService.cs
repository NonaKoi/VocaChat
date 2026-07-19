using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责好友自主私信 Session、轮次和逐条消息的持久化与生命周期更新。
/// </summary>
public sealed class AutonomousPrivateChatSessionService
{
    private const int SqliteUniqueConstraintErrorCode = 2067;
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousPrivateChatSessionService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 使用当时的概率设置快照，为一个已有 AI 好友私信创建运行中的 Session。
    /// </summary>
    public bool TryStartSession(
        Guid privateChatId,
        Guid initiatorAiAccountId,
        Guid recipientAiAccountId,
        string topic,
        int maximumRounds,
        int continuationRatePercent,
        DateTime startedAt,
        out AutonomousPrivateChatSession? session,
        out string errorMessage)
    {
        session = null;

        if (initiatorAiAccountId == recipientAiAccountId)
        {
            errorMessage = "自主私信需要两个不同的好友。";
            return false;
        }

        if (maximumRounds
                is < AutonomousInteractionSettings.MinimumPrivateChatMaximumRounds
                or > AutonomousInteractionSettings.MaximumPrivateChatMaximumRounds)
        {
            errorMessage = "自主私信最大轮数必须在 1 到 12 之间。";
            return false;
        }

        if (continuationRatePercent
                is < AutonomousInteractionSettings.MinimumPrivateChatContinuationRatePercent
                or > AutonomousInteractionSettings.MaximumPrivateChatContinuationRatePercent)
        {
            errorMessage = "下一轮概率保留比例必须在 0% 到 95% 之间。";
            return false;
        }

        string normalizedTopic = string.IsNullOrWhiteSpace(topic)
            ? "最近的生活"
            : topic.Trim();

        if (normalizedTopic.Length > AutonomousPrivateChatSession.TopicMaxLength)
        {
            errorMessage = $"自主私信话题不能超过 {AutonomousPrivateChatSession.TopicMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        PrivateChat? privateChat = dbContext.PrivateChats
            .AsNoTracking()
            .SingleOrDefault(chat => chat.Id == privateChatId);

        if (privateChat is null)
        {
            errorMessage = "好友私信不存在，不能创建自主交流。";
            return false;
        }

        if (!ParticipantsMatch(
                privateChat,
                initiatorAiAccountId,
                recipientAiAccountId))
        {
            errorMessage = "自主私信参与者必须是当前好友私信中的两个 AI 账号。";
            return false;
        }

        if (dbContext.AutonomousPrivateChatSessions.Any(item =>
                item.PrivateChatId == privateChatId
                && item.Status == AutonomousPrivateChatSessionStatus.Running))
        {
            errorMessage = "当前好友私信已经存在运行中的自主交流。";
            return false;
        }

        AutonomousPrivateChatSession newSession = new(
            privateChatId,
            initiatorAiAccountId,
            recipientAiAccountId,
            normalizedTopic,
            maximumRounds,
            continuationRatePercent,
            startedAt);
        dbContext.AutonomousPrivateChatSessions.Add(newSession);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            errorMessage = "当前好友私信已经存在运行中的自主交流。";
            return false;
        }

        session = newSession;
        errorMessage = string.Empty;
        return true;
    }

    public AutonomousPrivateChatSession? FindById(Guid sessionId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.AutonomousPrivateChatSessions
            .AsNoTracking()
            .SingleOrDefault(session => session.Id == sessionId);
    }

    public AutonomousPrivateChatSession? FindLatestByPrivateChatId(
        Guid privateChatId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.AutonomousPrivateChatSessions
            .AsNoTracking()
            .Where(session => session.PrivateChatId == privateChatId)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .FirstOrDefault();
    }

    public IReadOnlyList<AutonomousPrivateChatRound> GetRounds(Guid sessionId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.AutonomousPrivateChatRounds
            .AsNoTracking()
            .Where(round => round.SessionId == sessionId)
            .OrderBy(round => round.RoundNumber)
            .ThenBy(round => round.Id)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<PrivateMessage> GetMessages(Guid sessionId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.PrivateMessages
            .AsNoTracking()
            .Where(message =>
                message.AutonomousPrivateChatSessionId == sessionId)
            .OrderBy(message => message.AutonomousSequenceNumber ?? int.MaxValue)
            .ThenBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 保存已经决定发生的普通轮，或只执行一次的收束轮。
    /// </summary>
    public bool TryStartRound(
        Guid sessionId,
        bool isClosing,
        double? occurrenceProbability,
        double? randomRoll,
        AutonomousPrivateChatMessageMode initiatorMessageMode,
        AutonomousPrivateChatMessageMode recipientMessageMode,
        int initiatorMessageCount,
        int recipientMessageCount,
        DateTime startedAt,
        out AutonomousPrivateChatRound? round,
        out string errorMessage)
    {
        round = null;

        if (!TryValidateRoundPlan(
                isClosing,
                occurrenceProbability,
                randomRoll,
                initiatorMessageMode,
                recipientMessageMode,
                initiatorMessageCount,
                recipientMessageCount,
                out errorMessage))
        {
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousPrivateChatSession? session =
            dbContext.AutonomousPrivateChatSessions
                .SingleOrDefault(item => item.Id == sessionId);

        if (session is null)
        {
            errorMessage = "自主私信 Session 不存在，不能创建轮次。";
            return false;
        }

        if (session.Status != AutonomousPrivateChatSessionStatus.Running)
        {
            errorMessage = "自主私信已经结束，不能继续创建轮次。";
            return false;
        }

        List<AutonomousPrivateChatRound> existingRounds =
            dbContext.AutonomousPrivateChatRounds
                .Where(item => item.SessionId == sessionId)
                .OrderBy(item => item.RoundNumber)
                .ToList();

        if (existingRounds.Any(item => item.CompletedAt is null))
        {
            errorMessage = "当前自主私信仍有未完成的轮次。";
            return false;
        }

        if (existingRounds.Any(item => item.IsClosing))
        {
            errorMessage = "当前自主私信已经创建过收束轮。";
            return false;
        }

        if (!isClosing && session.CompletedRounds >= session.MaximumRounds)
        {
            errorMessage = "当前自主私信已经达到最大轮数。";
            return false;
        }

        if (!isClosing && session.CompletedRounds == 0
            && (occurrenceProbability != 1 || randomRoll is not null))
        {
            errorMessage = "自主私信第一轮必须以 100% 概率直接发生。";
            return false;
        }

        if (!isClosing && session.CompletedRounds > 0
            && (occurrenceProbability is null
                || randomRoll is null
                || randomRoll >= occurrenceProbability))
        {
            errorMessage = "只有通过下一轮概率判断的普通轮才能被创建。";
            return false;
        }

        AutonomousPrivateChatRound newRound = new(
            sessionId,
            existingRounds.Count + 1,
            isClosing,
            occurrenceProbability,
            randomRoll,
            initiatorMessageMode,
            recipientMessageMode,
            initiatorMessageCount,
            recipientMessageCount,
            startedAt);
        dbContext.AutonomousPrivateChatRounds.Add(newRound);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException)
        {
            errorMessage = "自主私信轮次暂时无法保存，请重试。";
            return false;
        }

        round = newRound;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 为当前轮次逐条保存一条正式消息；此前成功保存的消息不会因后续失败回滚。
    /// </summary>
    public bool TryAppendMessage(
        Guid roundId,
        AiAccount sender,
        string content,
        DateTime sentAt,
        out PrivateMessage? message,
        out AutonomousPrivateChatSession? session,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(sender);
        message = null;
        session = null;

        if (!TryNormalizeMessageContent(
                content,
                out string normalizedContent,
                out errorMessage))
        {
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousPrivateChatRound? round =
            dbContext.AutonomousPrivateChatRounds
                .SingleOrDefault(item => item.Id == roundId);

        if (round is null)
        {
            errorMessage = "自主私信轮次不存在，不能保存消息。";
            return false;
        }

        AutonomousPrivateChatSession? storedSession =
            dbContext.AutonomousPrivateChatSessions
                .SingleOrDefault(item => item.Id == round.SessionId);

        if (storedSession is null)
        {
            errorMessage = "自主私信 Session 不存在，不能保存消息。";
            return false;
        }

        session = storedSession;

        if (storedSession.Status != AutonomousPrivateChatSessionStatus.Running
            || round.CompletedAt is not null)
        {
            errorMessage = "自主私信轮次已经结束，不能继续保存消息。";
            return false;
        }

        bool senderIsInitiator =
            sender.Id == storedSession.InitiatorAiAccountId;
        bool senderIsRecipient =
            sender.Id == storedSession.RecipientAiAccountId;

        if (!senderIsInitiator && !senderIsRecipient)
        {
            errorMessage = "消息发送者不属于当前自主私信。";
            return false;
        }

        int plannedMessageCount = senderIsInitiator
            ? round.InitiatorMessageCount
            : round.RecipientMessageCount;
        int savedInitiatorMessageCount = dbContext.PrivateMessages.Count(item =>
            item.AutonomousPrivateChatRoundId == roundId
            && item.SenderAiAccountId == storedSession.InitiatorAiAccountId);
        int savedRecipientMessageCount = dbContext.PrivateMessages.Count(item =>
            item.AutonomousPrivateChatRoundId == roundId
            && item.SenderAiAccountId == storedSession.RecipientAiAccountId);

        if (senderIsInitiator && savedRecipientMessageCount > 0)
        {
            errorMessage = "接收方已经开始回应，发起方不能再补写本轮消息。";
            return false;
        }

        if (senderIsRecipient
            && savedInitiatorMessageCount != round.InitiatorMessageCount)
        {
            errorMessage = "发起方尚未保存完本轮消息，接收方不能提前回应。";
            return false;
        }

        int savedMessageCount = dbContext.PrivateMessages.Count(item =>
            item.AutonomousPrivateChatRoundId == roundId
            && item.SenderAiAccountId == sender.Id);

        if (savedMessageCount >= plannedMessageCount)
        {
            errorMessage = "当前发言者已经保存了本轮计划的全部消息。";
            return false;
        }

        int nextSequenceNumber = (dbContext.PrivateMessages
            .Where(item =>
                item.AutonomousPrivateChatSessionId == storedSession.Id
                && item.AutonomousSequenceNumber != null)
            .Max(item => item.AutonomousSequenceNumber) ?? 0) + 1;
        PrivateMessage newMessage = new(
            storedSession.PrivateChatId,
            MessageSenderType.AiAccount,
            sender.Nickname,
            sender.Id,
            normalizedContent,
            sentAt,
            storedSession.Id,
            round.Id,
            nextSequenceNumber);

        storedSession.RecordMessageActivity(sentAt);
        dbContext.PrivateMessages.Add(newMessage);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException)
        {
            session = FindById(storedSession.Id);
            errorMessage = "自主私信消息暂时无法保存，请重试。";
            return false;
        }

        message = newMessage;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 在计划中的每条消息均已保存后完成轮次；只有普通轮会推进 Session 轮数。
    /// </summary>
    public bool TryCompleteRound(
        Guid roundId,
        DateTime completedAt,
        out AutonomousPrivateChatSession? session,
        out string errorMessage)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousPrivateChatRound? round =
            dbContext.AutonomousPrivateChatRounds
                .SingleOrDefault(item => item.Id == roundId);

        if (round is null)
        {
            session = null;
            errorMessage = "自主私信轮次不存在。";
            return false;
        }

        AutonomousPrivateChatSession? storedSession =
            dbContext.AutonomousPrivateChatSessions
                .SingleOrDefault(item => item.Id == round.SessionId);
        session = storedSession;

        if (storedSession is null
            || storedSession.Status != AutonomousPrivateChatSessionStatus.Running
            || round.CompletedAt is not null)
        {
            errorMessage = "自主私信轮次已经结束或不可用。";
            return false;
        }

        Dictionary<Guid, int> savedCounts = dbContext.PrivateMessages
            .Where(item => item.AutonomousPrivateChatRoundId == roundId)
            .GroupBy(item => item.SenderAiAccountId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        int initiatorCount = savedCounts.GetValueOrDefault(
            storedSession.InitiatorAiAccountId);
        int recipientCount = savedCounts.GetValueOrDefault(
            storedSession.RecipientAiAccountId);

        if (initiatorCount != round.InitiatorMessageCount
            || recipientCount != round.RecipientMessageCount)
        {
            errorMessage = "当前轮次仍有计划消息尚未保存。";
            return false;
        }

        try
        {
            round.Complete(completedAt);
            if (!round.IsClosing)
            {
                storedSession.RecordCompletedRound(completedAt);
            }
            else
            {
                storedSession.RecordMessageActivity(completedAt);
            }

            dbContext.SaveChanges();
        }
        catch (InvalidOperationException exception)
        {
            errorMessage = exception.Message;
            return false;
        }
        catch (DbUpdateException)
        {
            session = FindById(storedSession.Id);
            errorMessage = "自主私信轮次状态暂时无法保存，请重试。";
            return false;
        }

        session = storedSession;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryCompleteSession(
        Guid sessionId,
        AutonomousPrivateChatSessionEndReason endReason,
        DateTime endedAt,
        out AutonomousPrivateChatSession? session,
        out string errorMessage)
    {
        return TryEndSession(
            sessionId,
            endReason,
            endedAt,
            isFailure: false,
            out session,
            out errorMessage);
    }

    public bool TryFailSession(
        Guid sessionId,
        AutonomousPrivateChatSessionEndReason endReason,
        DateTime endedAt,
        out AutonomousPrivateChatSession? session,
        out string errorMessage)
    {
        return TryEndSession(
            sessionId,
            endReason,
            endedAt,
            isFailure: true,
            out session,
            out errorMessage);
    }

    private bool TryEndSession(
        Guid sessionId,
        AutonomousPrivateChatSessionEndReason endReason,
        DateTime endedAt,
        bool isFailure,
        out AutonomousPrivateChatSession? session,
        out string errorMessage)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousPrivateChatSession? storedSession =
            dbContext.AutonomousPrivateChatSessions
                .SingleOrDefault(item => item.Id == sessionId);

        if (storedSession is null)
        {
            session = null;
            errorMessage = "自主私信 Session 不存在。";
            return false;
        }

        if (storedSession.Status != AutonomousPrivateChatSessionStatus.Running)
        {
            session = storedSession;
            errorMessage = "自主私信已经结束，不能重复修改终态。";
            return false;
        }

        try
        {
            if (isFailure)
            {
                storedSession.Fail(endReason, endedAt);
            }
            else
            {
                storedSession.Complete(endReason, endedAt);
            }

            dbContext.SaveChanges();
        }
        catch (ArgumentException exception)
        {
            session = storedSession;
            errorMessage = exception.Message;
            return false;
        }
        catch (DbUpdateException)
        {
            session = FindById(sessionId);
            errorMessage = "自主私信状态暂时无法保存，请重试。";
            return false;
        }

        session = storedSession;
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateRoundPlan(
        bool isClosing,
        double? occurrenceProbability,
        double? randomRoll,
        AutonomousPrivateChatMessageMode initiatorMessageMode,
        AutonomousPrivateChatMessageMode recipientMessageMode,
        int initiatorMessageCount,
        int recipientMessageCount,
        out string errorMessage)
    {
        if (!Enum.IsDefined(initiatorMessageMode)
            || !Enum.IsDefined(recipientMessageMode))
        {
            errorMessage = "自主私信发言形式无效。";
            return false;
        }

        if (!MessageCountMatchesMode(
                initiatorMessageMode,
                initiatorMessageCount)
            || !MessageCountMatchesMode(
                recipientMessageMode,
                recipientMessageCount))
        {
            errorMessage = "自主私信消息数量与发言形式不一致。";
            return false;
        }

        if (!isClosing
            && initiatorMessageMode == AutonomousPrivateChatMessageMode.None)
        {
            errorMessage = "普通自主私信轮次必须由发起方发言。";
            return false;
        }

        if (isClosing
            && (occurrenceProbability is not null || randomRoll is not null))
        {
            errorMessage = "收束轮不参与下一轮概率判断。";
            return false;
        }

        if (!isClosing
            && (occurrenceProbability is < 0 or > 1
                || randomRoll is < 0 or >= 1))
        {
            errorMessage = "自主私信轮次概率或随机值无效。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool MessageCountMatchesMode(
        AutonomousPrivateChatMessageMode mode,
        int count)
    {
        return mode switch
        {
            AutonomousPrivateChatMessageMode.None => count == 0,
            AutonomousPrivateChatMessageMode.Single => count == 1,
            AutonomousPrivateChatMessageMode.Burst => count is 2 or 3,
            _ => false
        };
    }

    private static bool TryNormalizeMessageContent(
        string content,
        out string normalizedContent,
        out string errorMessage)
    {
        normalizedContent = string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "消息内容不能为空。";
            return false;
        }

        normalizedContent = content.Trim();

        if (normalizedContent.Length > PrivateMessage.ContentMaxLength)
        {
            errorMessage = $"消息内容不能超过 {PrivateMessage.ContentMaxLength} 个字符。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool ParticipantsMatch(
        PrivateChat privateChat,
        Guid initiatorAiAccountId,
        Guid recipientAiAccountId)
    {
        return privateChat.Kind == PrivateChatKind.AiAccounts
            && ((privateChat.FirstAiAccountId == initiatorAiAccountId
                    && privateChat.SecondAiAccountId == recipientAiAccountId)
                || (privateChat.FirstAiAccountId == recipientAiAccountId
                    && privateChat.SecondAiAccountId == initiatorAiAccountId));
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                == SqliteUniqueConstraintErrorCode;
    }
}

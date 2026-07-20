using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责自主好友群聊 Session、轮次、消息归属和生命周期持久化。
/// </summary>
public sealed class AutonomousGroupChatSessionService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousGroupChatSessionService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public bool TryStartSession(
        Guid groupChatId,
        Guid initiatorAiAccountId,
        IReadOnlyList<Guid> participantAiAccountIds,
        string topic,
        int maximumRounds,
        int continuationRatePercent,
        DateTime startedAt,
        out AutonomousGroupChatSession? session,
        out string errorMessage)
    {
        session = null;
        string normalizedTopic = topic?.Trim() ?? string.Empty;
        IReadOnlyList<Guid> distinctParticipantIds = participantAiAccountIds
            .Distinct()
            .ToList()
            .AsReadOnly();

        if (distinctParticipantIds.Count < 3)
        {
            errorMessage = "自主好友群聊至少需要三位好友。";
            return false;
        }

        if (normalizedTopic.Length is 0 or > AutonomousGroupChatSession.TopicMaxLength)
        {
            errorMessage =
                $"自主好友群聊话题必须为 1 到 {AutonomousGroupChatSession.TopicMaxLength} 个字符。";
            return false;
        }

        if (!distinctParticipantIds.Contains(initiatorAiAccountId))
        {
            errorMessage = "自主好友群聊发起者必须属于本次参与者。";
            return false;
        }

        if (maximumRounds
                is < AutonomousInteractionSettings.MinimumGroupChatMaximumRounds
                or > AutonomousInteractionSettings.MaximumGroupChatMaximumRounds)
        {
            errorMessage = "自主好友群聊最大轮数必须在 1 到 12 之间。";
            return false;
        }

        if (continuationRatePercent
                is < AutonomousInteractionSettings.MinimumGroupChatContinuationRatePercent
                or > AutonomousInteractionSettings.MaximumGroupChatContinuationRatePercent)
        {
            errorMessage = "自主好友群聊下一轮概率保留比例必须在 0% 到 95% 之间。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        GroupChat? groupChat = dbContext.GroupChats
            .Include(storedGroupChat => storedGroupChat.Members)
            .SingleOrDefault(storedGroupChat => storedGroupChat.Id == groupChatId);

        if (groupChat is null)
        {
            errorMessage = "好友群聊不存在，不能开始自主互动。";
            return false;
        }

        if (groupChat.IncludesLocalUser)
        {
            errorMessage = "包含本地用户的群聊不能作为自主好友群聊执行。";
            return false;
        }

        HashSet<Guid> expectedIds = distinctParticipantIds.ToHashSet();
        if (groupChat.Members.Count != expectedIds.Count
            || groupChat.Members.Any(member => !expectedIds.Contains(member.Id)))
        {
            errorMessage = "本次参与者必须与好友群聊的成员完全一致。";
            return false;
        }

        bool hasRunningSession = dbContext.AutonomousGroupChatSessions.Any(
            storedSession =>
                storedSession.GroupChatId == groupChatId
                && storedSession.Status == AutonomousGroupChatSessionStatus.Running);
        if (hasRunningSession)
        {
            errorMessage = "当前好友群聊已经存在运行中的自主互动。";
            return false;
        }

        AutonomousGroupChatSession newSession = new(
            groupChatId,
            initiatorAiAccountId,
            normalizedTopic,
            groupChat.Members,
            maximumRounds,
            continuationRatePercent,
            startedAt);
        dbContext.AutonomousGroupChatSessions.Add(newSession);
        dbContext.SaveChanges();

        session = newSession;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryStartRound(
        Guid sessionId,
        bool isClosing,
        double? occurrenceProbability,
        double? randomRoll,
        int plannedSpeakerCount,
        int plannedMessageCount,
        DateTime startedAt,
        out AutonomousGroupChatRound? round,
        out string errorMessage)
    {
        round = null;

        if (!TryValidateRoundPlan(
                isClosing,
                occurrenceProbability,
                randomRoll,
                plannedSpeakerCount,
                plannedMessageCount,
                out errorMessage))
        {
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousGroupChatSession? session =
            dbContext.AutonomousGroupChatSessions
                .SingleOrDefault(item => item.Id == sessionId);
        if (session is null)
        {
            errorMessage = "自主好友群聊 Session 不存在。";
            return false;
        }

        if (session.Status != AutonomousGroupChatSessionStatus.Running)
        {
            errorMessage = "自主好友群聊已经结束，不能开始新轮次。";
            return false;
        }

        if (!isClosing && session.CompletedRounds >= session.MaximumRounds)
        {
            errorMessage = "自主好友群聊已经达到最大普通轮数。";
            return false;
        }

        bool hasUnfinishedRound = dbContext.AutonomousGroupChatRounds.Any(
            item => item.SessionId == sessionId && item.CompletedAt == null);
        if (hasUnfinishedRound)
        {
            errorMessage = "当前自主好友群聊仍有未完成轮次。";
            return false;
        }

        AutonomousGroupChatRound newRound = new(
            sessionId,
            session.CompletedRounds + 1,
            isClosing,
            occurrenceProbability,
            randomRoll,
            plannedSpeakerCount,
            plannedMessageCount,
            startedAt);
        dbContext.AutonomousGroupChatRounds.Add(newRound);
        dbContext.SaveChanges();

        round = newRound;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryAppendMessage(
        Guid roundId,
        AiAccount sender,
        string content,
        DateTime sentAt,
        out GroupMessage? message,
        out AutonomousGroupChatSession? session,
        out string errorMessage)
    {
        message = null;
        session = null;
        string normalizedContent = content?.Trim() ?? string.Empty;

        if (normalizedContent.Length is 0 or > GroupMessage.ContentMaxLength)
        {
            errorMessage =
                $"群消息内容必须为 1 到 {GroupMessage.ContentMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousGroupChatRound? round = dbContext.AutonomousGroupChatRounds
            .SingleOrDefault(item => item.Id == roundId);
        if (round is null)
        {
            errorMessage = "自主好友群聊轮次不存在。";
            return false;
        }

        AutonomousGroupChatSession? storedSession =
            dbContext.AutonomousGroupChatSessions
                .Include(item => item.Participants)
                .SingleOrDefault(item => item.Id == round.SessionId);
        session = storedSession;

        if (storedSession is null
            || storedSession.Status != AutonomousGroupChatSessionStatus.Running
            || round.CompletedAt is not null)
        {
            errorMessage = "自主好友群聊轮次已经结束或不可用。";
            return false;
        }

        if (!storedSession.Participants.Any(account => account.Id == sender.Id))
        {
            errorMessage = "只有本次自主好友群聊的参与者才能发送消息。";
            return false;
        }

        int savedMessageCount = dbContext.GroupMessages.Count(item =>
            item.AutonomousGroupChatRoundId == roundId);
        if (savedMessageCount >= round.PlannedMessageCount)
        {
            errorMessage = "当前轮次的计划消息已经全部保存。";
            return false;
        }

        GroupMessage newMessage = new(
            storedSession.GroupChatId,
            MessageSenderType.AiAccount,
            sender.Nickname,
            sender.Id,
            normalizedContent,
            sentAt,
            storedSession.Id,
            round.Id,
            sequenceNumber: (dbContext.GroupMessages
                .Where(item => item.GroupChatId == storedSession.GroupChatId)
                .Max(item => (long?)item.SequenceNumber) ?? 0) + 1);
        storedSession.RecordMessageActivity(sentAt);
        dbContext.GroupMessages.Add(newMessage);
        dbContext.SaveChanges();

        message = newMessage;
        session = storedSession;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryCompleteRound(
        Guid roundId,
        DateTime completedAt,
        out AutonomousGroupChatSession? session,
        out string errorMessage)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousGroupChatRound? round = dbContext.AutonomousGroupChatRounds
            .SingleOrDefault(item => item.Id == roundId);
        if (round is null)
        {
            session = null;
            errorMessage = "自主好友群聊轮次不存在。";
            return false;
        }

        AutonomousGroupChatSession? storedSession =
            dbContext.AutonomousGroupChatSessions
                .SingleOrDefault(item => item.Id == round.SessionId);
        session = storedSession;
        if (storedSession is null
            || storedSession.Status != AutonomousGroupChatSessionStatus.Running
            || round.CompletedAt is not null)
        {
            errorMessage = "自主好友群聊轮次已经结束或不可用。";
            return false;
        }

        int savedMessageCount = dbContext.GroupMessages.Count(item =>
            item.AutonomousGroupChatRoundId == roundId);
        if (savedMessageCount != round.PlannedMessageCount)
        {
            errorMessage = "当前轮次仍有计划消息尚未保存。";
            return false;
        }

        round.Complete(completedAt);
        if (round.IsClosing)
        {
            storedSession.RecordMessageActivity(completedAt);
        }
        else
        {
            storedSession.RecordCompletedRound(completedAt);
        }

        dbContext.SaveChanges();
        session = storedSession;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryCompleteSession(
        Guid sessionId,
        AutonomousGroupChatSessionEndReason endReason,
        DateTime endedAt,
        out AutonomousGroupChatSession? session,
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
        AutonomousGroupChatSessionEndReason endReason,
        DateTime endedAt,
        out AutonomousGroupChatSession? session,
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

    public AutonomousGroupChatSession? FindById(Guid sessionId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.AutonomousGroupChatSessions
            .AsNoTracking()
            .Include(session => session.Participants)
            .SingleOrDefault(session => session.Id == sessionId);
    }

    public IReadOnlyList<AutonomousGroupChatRound> GetRounds(Guid sessionId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.AutonomousGroupChatRounds
            .AsNoTracking()
            .Where(round => round.SessionId == sessionId)
            .OrderBy(round => round.RoundNumber)
            .ToList()
            .AsReadOnly();
    }

    public AutonomousGroupChatRound? FindRoundById(Guid roundId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.AutonomousGroupChatRounds
            .AsNoTracking()
            .SingleOrDefault(round => round.Id == roundId);
    }

    private bool TryEndSession(
        Guid sessionId,
        AutonomousGroupChatSessionEndReason endReason,
        DateTime endedAt,
        bool isFailure,
        out AutonomousGroupChatSession? session,
        out string errorMessage)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousGroupChatSession? storedSession =
            dbContext.AutonomousGroupChatSessions
                .Include(item => item.Participants)
                .SingleOrDefault(item => item.Id == sessionId);

        if (storedSession is null)
        {
            session = null;
            errorMessage = "自主好友群聊 Session 不存在。";
            return false;
        }

        if (storedSession.Status != AutonomousGroupChatSessionStatus.Running)
        {
            session = storedSession;
            errorMessage = "自主好友群聊已经结束。";
            return false;
        }

        bool hasUnfinishedRound = dbContext.AutonomousGroupChatRounds.Any(
            round => round.SessionId == sessionId && round.CompletedAt == null);
        if (hasUnfinishedRound && !isFailure)
        {
            session = storedSession;
            errorMessage = "自主好友群聊仍有未完成轮次。";
            return false;
        }

        try
        {
            if (!isFailure)
            {
                storedSession.Complete(endReason, endedAt);
            }
            else
            {
                storedSession.Fail(endReason, endedAt);
            }

            dbContext.SaveChanges();
        }
        catch (ArgumentException exception)
        {
            session = storedSession;
            errorMessage = exception.Message;
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
        int plannedSpeakerCount,
        int plannedMessageCount,
        out string errorMessage)
    {
        if (plannedSpeakerCount is < 0 or > 3
            || plannedMessageCount is < 0 or > 9
            || plannedMessageCount < plannedSpeakerCount)
        {
            errorMessage = "自主好友群聊轮次的发言者或消息数量无效。";
            return false;
        }

        if (!isClosing && (plannedSpeakerCount == 0 || plannedMessageCount == 0))
        {
            errorMessage = "普通自主好友群聊轮次必须至少有一位发言者。";
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
            errorMessage = "自主好友群聊轮次概率或随机值无效。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}

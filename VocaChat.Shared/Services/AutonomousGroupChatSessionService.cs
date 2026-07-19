using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责自主好友群聊 Session 的创建、消息归属和生命周期持久化。
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
            startedAt);
        dbContext.AutonomousGroupChatSessions.Add(newSession);
        dbContext.SaveChanges();

        session = newSession;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryAppendMessage(
        Guid sessionId,
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
        AutonomousGroupChatSession? storedSession =
            dbContext.AutonomousGroupChatSessions
                .Include(item => item.Participants)
                .SingleOrDefault(item => item.Id == sessionId);

        if (storedSession is null)
        {
            errorMessage = "自主好友群聊 Session 不存在。";
            return false;
        }

        if (storedSession.Status != AutonomousGroupChatSessionStatus.Running)
        {
            errorMessage = "自主好友群聊已经结束，不能继续保存消息。";
            return false;
        }

        if (!storedSession.Participants.Any(account => account.Id == sender.Id))
        {
            errorMessage = "只有本次自主好友群聊的参与者才能发送消息。";
            return false;
        }

        GroupMessage newMessage = new(
            storedSession.GroupChatId,
            MessageSenderType.AiAccount,
            sender.Nickname,
            sender.Id,
            normalizedContent,
            sentAt,
            storedSession.Id);
        storedSession.RecordMessageActivity(sentAt);
        dbContext.GroupMessages.Add(newMessage);
        dbContext.SaveChanges();

        message = newMessage;
        session = storedSession;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryCompleteSession(
        Guid sessionId,
        DateTime endedAt,
        out AutonomousGroupChatSession? session,
        out string errorMessage)
    {
        return TryEndSession(
            sessionId,
            AutonomousGroupChatSessionEndReason.Completed,
            endedAt,
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

    private bool TryEndSession(
        Guid sessionId,
        AutonomousGroupChatSessionEndReason endReason,
        DateTime endedAt,
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

        if (endReason == AutonomousGroupChatSessionEndReason.Completed)
        {
            storedSession.Complete(endedAt);
        }
        else
        {
            storedSession.Fail(endReason, endedAt);
        }

        dbContext.SaveChanges();
        session = storedSession;
        errorMessage = string.Empty;
        return true;
    }
}

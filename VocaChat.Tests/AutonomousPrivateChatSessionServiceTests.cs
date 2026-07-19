using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证好友自主私信 Session、轮次和逐条消息的持久化边界。
/// </summary>
public sealed class AutonomousPrivateChatSessionServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void StartSession_PersistsSettingsSnapshotAcrossServiceInstances()
    {
        (AiAccount first, AiAccount second, PrivateChat chat) =
            CreateAiPrivateChat("Persist");
        DateTime startedAt = new(2026, 7, 18, 14, 0, 0);

        bool created = CreateService().TryStartSession(
            chat.Id,
            first.Id,
            second.Id,
            "摄影",
            maximumRounds: 8,
            continuationRatePercent: 65,
            startedAt,
            out AutonomousPrivateChatSession? session,
            out string errorMessage);

        Assert.True(created, errorMessage);
        AutonomousPrivateChatSession storedSession = Assert.IsType<
            AutonomousPrivateChatSession>(CreateService().FindById(session!.Id));
        Assert.Equal(AutonomousPrivateChatSessionStatus.Running, storedSession.Status);
        Assert.Equal("摄影", storedSession.Topic);
        Assert.Equal(8, storedSession.MaximumRounds);
        Assert.Equal(65, storedSession.ContinuationRatePercent);
        Assert.Equal(0, storedSession.CompletedRounds);
        Assert.Equal(startedAt, storedSession.StartedAt);
    }

    [Fact]
    public void StartSession_WithMismatchedParticipant_IsRejected()
    {
        (AiAccount first, _, PrivateChat chat) = CreateAiPrivateChat("Mismatch");
        AiAccount unrelated = CreateAccount("MismatchOther");

        bool created = CreateService().TryStartSession(
            chat.Id,
            first.Id,
            unrelated.Id,
            "近况",
            maximumRounds: 6,
            continuationRatePercent: 80,
            DateTime.Now,
            out AutonomousPrivateChatSession? session,
            out string errorMessage);

        Assert.False(created);
        Assert.Null(session);
        Assert.Contains("参与者", errorMessage);
    }

    [Fact]
    public void StartSession_WhenOneIsRunning_IsRejectedUntilItEnds()
    {
        (AiAccount first, AiAccount second, PrivateChat chat) =
            CreateAiPrivateChat("Conflict");
        AutonomousPrivateChatSessionService service = CreateService();
        Assert.True(service.TryStartSession(
            chat.Id,
            first.Id,
            second.Id,
            "近况",
            maximumRounds: 6,
            continuationRatePercent: 80,
            DateTime.Now,
            out AutonomousPrivateChatSession? firstSession,
            out string firstError), firstError);

        Assert.False(service.TryStartSession(
            chat.Id,
            second.Id,
            first.Id,
            "下一次",
            maximumRounds: 6,
            continuationRatePercent: 80,
            DateTime.Now.AddMinutes(1),
            out _,
            out string conflictError));
        Assert.Contains("运行中", conflictError);

        Assert.True(service.TryFailSession(
            firstSession!.Id,
            AutonomousPrivateChatSessionEndReason.GenerationFailed,
            DateTime.Now.AddMinutes(2),
            out _,
            out string failureError), failureError);
        Assert.True(service.TryStartSession(
            chat.Id,
            second.Id,
            first.Id,
            "下一次",
            maximumRounds: 6,
            continuationRatePercent: 80,
            DateTime.Now.AddMinutes(3),
            out _,
            out string retryError), retryError);
    }

    [Fact]
    public void RoundMessages_AreSavedIndividuallyWithStableSequence()
    {
        (AiAccount first, AiAccount second, PrivateChat chat) =
            CreateAiPrivateChat("Round");
        AutonomousPrivateChatSessionService service = CreateService();
        DateTime occurredAt = new(2026, 7, 18, 15, 0, 0);
        AutonomousPrivateChatSession session = StartSession(
            service, chat, first, second, occurredAt);

        Assert.True(service.TryStartRound(
            session.Id,
            isClosing: false,
            occurrenceProbability: 1,
            randomRoll: null,
            AutonomousPrivateChatMessageMode.Burst,
            AutonomousPrivateChatMessageMode.Single,
            initiatorMessageCount: 2,
            recipientMessageCount: 1,
            occurredAt,
            out AutonomousPrivateChatRound? round,
            out string roundError), roundError);

        PrivateMessage firstMessage = Append(
            service, round!, first, "最近在读什么？", occurredAt.AddTicks(1));
        PrivateMessage secondMessage = Append(
            service, round!, first, "我刚找到一本短篇集。", occurredAt.AddTicks(2));
        PrivateMessage reply = Append(
            service, round!, second, "我也想看看。", occurredAt.AddTicks(3));
        Assert.True(service.TryCompleteRound(
            round!.Id,
            occurredAt.AddTicks(4),
            out AutonomousPrivateChatSession? progressedSession,
            out string completeError), completeError);

        Assert.Equal(1, progressedSession!.CompletedRounds);
        Assert.Equal(new int?[] { 1, 2, 3 }, new[]
        {
            firstMessage.AutonomousSequenceNumber,
            secondMessage.AutonomousSequenceNumber,
            reply.AutonomousSequenceNumber
        });
        Assert.All(
            new[] { firstMessage, secondMessage, reply },
            message => Assert.Equal(round.Id, message.AutonomousPrivateChatRoundId));

        IReadOnlyList<PrivateMessage> storedMessages =
            CreateService().GetMessages(session.Id);
        Assert.Equal(
            new[] { firstMessage.Id, secondMessage.Id, reply.Id },
            storedMessages.Select(message => message.Id));
    }

    [Fact]
    public void InvalidLaterMessage_DoesNotRollbackPreviouslySavedMessage()
    {
        (AiAccount first, AiAccount second, PrivateChat chat) =
            CreateAiPrivateChat("Partial");
        AutonomousPrivateChatSessionService service = CreateService();
        DateTime occurredAt = new(2026, 7, 18, 16, 0, 0);
        AutonomousPrivateChatSession session = StartSession(
            service, chat, first, second, occurredAt);

        Assert.True(service.TryStartRound(
            session.Id,
            isClosing: false,
            occurrenceProbability: 1,
            randomRoll: null,
            AutonomousPrivateChatMessageMode.Single,
            AutonomousPrivateChatMessageMode.Single,
            initiatorMessageCount: 1,
            recipientMessageCount: 1,
            occurredAt,
            out AutonomousPrivateChatRound? round,
            out string roundError), roundError);
        PrivateMessage savedMessage = Append(
            service, round!, first, "这条消息应当保留。", occurredAt.AddTicks(1));

        bool savedBlankReply = service.TryAppendMessage(
            round!.Id,
            second,
            "   ",
            occurredAt.AddTicks(2),
            out _,
            out _,
            out string errorMessage);

        Assert.False(savedBlankReply);
        Assert.Contains("不能为空", errorMessage);
        Assert.Equal(
            new[] { savedMessage.Id },
            CreateService().GetMessages(session.Id).Select(message => message.Id));
        Assert.Equal(0, CreateService().FindById(session.Id)!.CompletedRounds);
    }

    [Fact]
    public void Recipient_CannotReplyBeforeInitiatorCompletesBurst()
    {
        (AiAccount first, AiAccount second, PrivateChat chat) =
            CreateAiPrivateChat("Order");
        AutonomousPrivateChatSessionService service = CreateService();
        DateTime occurredAt = new(2026, 7, 18, 17, 0, 0);
        AutonomousPrivateChatSession session = StartSession(
            service, chat, first, second, occurredAt);

        Assert.True(service.TryStartRound(
            session.Id,
            false,
            1,
            null,
            AutonomousPrivateChatMessageMode.Burst,
            AutonomousPrivateChatMessageMode.Single,
            2,
            1,
            occurredAt,
            out AutonomousPrivateChatRound? round,
            out string roundError), roundError);
        Append(service, round!, first, "第一句", occurredAt.AddTicks(1));

        Assert.False(service.TryAppendMessage(
            round!.Id,
            second,
            "过早回复",
            occurredAt.AddTicks(2),
            out _,
            out _,
            out string errorMessage));
        Assert.Contains("不能提前回应", errorMessage);
    }

    [Fact]
    public void OrdinaryAiMessage_DoesNotRequireSession()
    {
        (AiAccount first, _, PrivateChat chat) = CreateAiPrivateChat("Ordinary");
        PrivateChatService privateChatService = new(
            _database.CreateDbContextFactory());

        bool saved = privateChatService.TrySaveAiReply(
            chat,
            first,
            "普通私信消息",
            out PrivateMessage? message,
            out string errorMessage);

        Assert.True(saved, errorMessage);
        Assert.Null(message!.AutonomousPrivateChatSessionId);
        Assert.Null(message.AutonomousPrivateChatRoundId);
        Assert.Null(message.AutonomousSequenceNumber);
    }

    private AutonomousPrivateChatSession StartSession(
        AutonomousPrivateChatSessionService service,
        PrivateChat chat,
        AiAccount first,
        AiAccount second,
        DateTime startedAt)
    {
        Assert.True(service.TryStartSession(
            chat.Id,
            first.Id,
            second.Id,
            "阅读",
            maximumRounds: 6,
            continuationRatePercent: 80,
            startedAt,
            out AutonomousPrivateChatSession? session,
            out string errorMessage), errorMessage);
        return Assert.IsType<AutonomousPrivateChatSession>(session);
    }

    private static PrivateMessage Append(
        AutonomousPrivateChatSessionService service,
        AutonomousPrivateChatRound round,
        AiAccount sender,
        string content,
        DateTime sentAt)
    {
        Assert.True(service.TryAppendMessage(
            round.Id,
            sender,
            content,
            sentAt,
            out PrivateMessage? message,
            out _,
            out string errorMessage), errorMessage);
        return Assert.IsType<PrivateMessage>(message);
    }

    private AutonomousPrivateChatSessionService CreateService()
    {
        return new AutonomousPrivateChatSessionService(
            _database.CreateDbContextFactory());
    }

    private (AiAccount First, AiAccount Second, PrivateChat Chat)
        CreateAiPrivateChat(string prefix)
    {
        AiAccount first = CreateAccount($"{prefix}A");
        AiAccount second = CreateAccount($"{prefix}B");
        PrivateChatService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryGetOrCreateAiPrivateChat(
            first.Id,
            second.Id,
            out PrivateChat? chat,
            out _,
            out string errorMessage), errorMessage);
        return (first, second, Assert.IsType<PrivateChat>(chat));
    }

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

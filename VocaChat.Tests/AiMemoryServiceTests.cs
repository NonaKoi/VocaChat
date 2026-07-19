using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证方向记忆的来源约束、持久化、筛选顺序和幂等保护。
/// </summary>
public sealed class AiMemoryServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void CompletedSessionMemory_PersistsOnlyForSpecifiedDirection()
    {
        (AiAccount first, AiAccount second) = CreatePair("Direction");
        AutonomousPrivateChatSession session = CreateEndedSession(
            first,
            second,
            new DateTime(2026, 7, 19, 17, 0, 0));
        AiMemoryService service = CreateService();

        AiMemoryOperationStatus createStatus = service.TryCreateMemory(
            first.Id,
            second.Id,
            AiMemoryType.Preference,
            "  对方喜欢在雨天散步  ",
            80,
            session.PrivateChatId,
            session.Id,
            session.EndedAt!.Value,
            out AiMemory? created,
            out string createError);

        Assert.Equal(AiMemoryOperationStatus.Success, createStatus);
        Assert.Equal(string.Empty, createError);
        Assert.NotNull(created);
        Assert.Equal("对方喜欢在雨天散步", created.Summary);
        Assert.Equal(first.Id, created.OwnerAiAccountId);
        Assert.Equal(second.Id, created.SubjectAiAccountId);
        Assert.True(created.IsActive);
        Assert.Null(created.LastRecalledAt);

        AiMemoryService restartedService = CreateService();
        Assert.Equal(
            AiMemoryOperationStatus.Success,
            restartedService.TryGetActiveMemories(
                first.Id,
                second.Id,
                10,
                type: null,
                out IReadOnlyList<AiMemory> firstDirection,
                out string queryError));
        Assert.Equal(string.Empty, queryError);
        AiMemory reloaded = Assert.Single(firstDirection);
        Assert.Equal(created.Id, reloaded.Id);
        Assert.Equal(session.Id, reloaded.SourceSessionId);

        Assert.Equal(
            AiMemoryOperationStatus.Success,
            restartedService.TryGetActiveMemories(
                second.Id,
                first.Id,
                10,
                type: null,
                out IReadOnlyList<AiMemory> reverseDirection,
                out _));
        Assert.Empty(reverseDirection);
    }

    [Fact]
    public void ActiveMemoryQuery_FiltersTypeAndOrdersCandidates()
    {
        (AiAccount first, AiAccount second) = CreatePair("Order");
        DateTime endedAt = new(2026, 7, 19, 18, 0, 0);
        AutonomousPrivateChatSession session = CreateEndedSession(
            first,
            second,
            endedAt);
        AiMemoryService service = CreateService();

        CreateMemory(
            service,
            first,
            second,
            session,
            AiMemoryType.Habit,
            "经常晚睡",
            50,
            endedAt.AddDays(-2));
        CreateMemory(
            service,
            first,
            second,
            session,
            AiMemoryType.Preference,
            "喜欢清淡的食物",
            90,
            endedAt.AddDays(-1));
        CreateMemory(
            service,
            first,
            second,
            session,
            AiMemoryType.Preference,
            "不喜欢拥挤的地方",
            90,
            endedAt);

        Assert.Equal(
            AiMemoryOperationStatus.Success,
            service.TryGetActiveMemories(
                first.Id,
                second.Id,
                2,
                type: null,
                out IReadOnlyList<AiMemory> topMemories,
                out _));
        Assert.Equal(
            ["不喜欢拥挤的地方", "喜欢清淡的食物"],
            topMemories.Select(memory => memory.Summary));

        Assert.Equal(
            AiMemoryOperationStatus.Success,
            service.TryGetActiveMemories(
                first.Id,
                second.Id,
                10,
                AiMemoryType.Habit,
                out IReadOnlyList<AiMemory> habits,
                out _));
        Assert.Equal("经常晚睡", Assert.Single(habits).Summary);
    }

    [Fact]
    public void DuplicateRetry_ReturnsExistingMemoryWithoutSecondInsert()
    {
        (AiAccount first, AiAccount second) = CreatePair("Duplicate");
        AutonomousPrivateChatSession session = CreateEndedSession(
            first,
            second,
            new DateTime(2026, 7, 19, 19, 0, 0));
        AiMemoryService service = CreateService();

        AiMemory firstMemory = CreateMemory(
            service,
            first,
            second,
            session,
            AiMemoryType.SharedExperience,
            "一起讨论过周末计划",
            70,
            session.EndedAt!.Value);
        AiMemoryOperationStatus secondStatus = service.TryCreateMemory(
            first.Id,
            second.Id,
            AiMemoryType.SharedExperience,
            "  一起讨论过周末计划  ",
            95,
            session.PrivateChatId,
            session.Id,
            session.EndedAt.Value,
            out AiMemory? repeated,
            out string errorMessage);

        Assert.Equal(AiMemoryOperationStatus.AlreadyExists, secondStatus);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(firstMemory.Id, repeated!.Id);
        Assert.Equal(70, repeated.Salience);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            1,
            dbContext.AiMemories.Count(memory =>
                memory.SourceSessionId == session.Id));
    }

    [Fact]
    public void InvalidInputAndMismatchedSources_AreRejected()
    {
        (AiAccount first, AiAccount second) = CreatePair("Invalid");
        AiAccount third = CreateAccount("InvalidThird");
        AutonomousPrivateChatSession session = CreateEndedSession(
            first,
            second,
            new DateTime(2026, 7, 19, 20, 0, 0));
        AiMemoryService service = CreateService();

        AssertCreateStatus(
            AiMemoryOperationStatus.SelfMemoryNotAllowed,
            service,
            first.Id,
            first.Id,
            AiMemoryType.Preference,
            "有效摘要",
            50,
            session);
        AssertCreateStatus(
            AiMemoryOperationStatus.InvalidType,
            service,
            first.Id,
            second.Id,
            (AiMemoryType)99,
            "有效摘要",
            50,
            session);
        AssertCreateStatus(
            AiMemoryOperationStatus.InvalidSummary,
            service,
            first.Id,
            second.Id,
            AiMemoryType.Preference,
            "   ",
            50,
            session);
        AssertCreateStatus(
            AiMemoryOperationStatus.InvalidSalience,
            service,
            first.Id,
            second.Id,
            AiMemoryType.Preference,
            "有效摘要",
            0,
            session);
        AssertCreateStatus(
            AiMemoryOperationStatus.AccountNotFound,
            service,
            first.Id,
            Guid.NewGuid(),
            AiMemoryType.Preference,
            "有效摘要",
            50,
            session);
        AssertCreateStatus(
            AiMemoryOperationStatus.SourceMismatch,
            service,
            first.Id,
            third.Id,
            AiMemoryType.Preference,
            "有效摘要",
            50,
            session);

        Assert.Equal(
            AiMemoryOperationStatus.SourceNotFound,
            service.TryCreateMemory(
                first.Id,
                second.Id,
                AiMemoryType.Preference,
                "有效摘要",
                50,
                Guid.NewGuid(),
                Guid.NewGuid(),
                session.EndedAt!.Value,
                out _,
                out _));

        AutonomousPrivateChatSession runningSession = CreateRunningSession(
            first,
            third,
            new DateTime(2026, 7, 19, 20, 30, 0));
        AssertCreateStatus(
            AiMemoryOperationStatus.SessionNotEligible,
            service,
            first.Id,
            third.Id,
            AiMemoryType.Preference,
            "仍在进行的交流",
            50,
            runningSession);

        Assert.Equal(
            AiMemoryOperationStatus.InvalidLimit,
            service.TryGetActiveMemories(
                first.Id,
                second.Id,
                0,
                type: null,
                out _,
                out _));
    }

    [Fact]
    public void PartiallyFailedSessionWithCompletedRound_CanProvideMemory()
    {
        (AiAccount first, AiAccount second) = CreatePair("Partial");
        AutonomousPrivateChatSession session = CreateEndedSession(
            first,
            second,
            new DateTime(2026, 7, 19, 21, 0, 0),
            failAfterRound: true);

        AiMemory memory = CreateMemory(
            CreateService(),
            second,
            first,
            session,
            AiMemoryType.ImportantEvent,
            "对话在一次有效交流后中断",
            60,
            session.EndedAt!.Value);

        Assert.Equal(second.Id, memory.OwnerAiAccountId);
        Assert.Equal(first.Id, memory.SubjectAiAccountId);
        Assert.Equal(
            AutonomousPrivateChatSessionStatus.Failed,
            session.Status);
    }

    private AutonomousPrivateChatSession CreateEndedSession(
        AiAccount initiator,
        AiAccount recipient,
        DateTime endedAt,
        bool failAfterRound = false)
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        PrivateChatService privateChatService = new(factory);
        Assert.True(privateChatService.TryGetOrCreateAiPrivateChat(
            initiator.Id,
            recipient.Id,
            out PrivateChat? privateChat,
            out _,
            out string chatError), chatError);

        AutonomousPrivateChatSessionService sessionService = new(factory);
        Assert.True(sessionService.TryStartSession(
            privateChat!.Id,
            initiator.Id,
            recipient.Id,
            "近况",
            maximumRounds: 3,
            continuationRatePercent: 80,
            endedAt.AddMinutes(-3),
            out AutonomousPrivateChatSession? session,
            out string sessionError), sessionError);
        Assert.True(sessionService.TryStartRound(
            session!.Id,
            isClosing: false,
            occurrenceProbability: 1,
            randomRoll: null,
            AutonomousPrivateChatMessageMode.Single,
            AutonomousPrivateChatMessageMode.None,
            initiatorMessageCount: 1,
            recipientMessageCount: 0,
            endedAt.AddMinutes(-2),
            out AutonomousPrivateChatRound? round,
            out string roundError), roundError);
        Assert.True(sessionService.TryAppendMessage(
            round!.Id,
            initiator,
            "最近发生了一件值得记住的事",
            endedAt.AddMinutes(-1),
            out _,
            out _,
            out string messageError), messageError);
        Assert.True(sessionService.TryCompleteRound(
            round.Id,
            endedAt.AddSeconds(-30),
            out _,
            out string roundCompletionError), roundCompletionError);

        bool ended = failAfterRound
            ? sessionService.TryFailSession(
                session.Id,
                AutonomousPrivateChatSessionEndReason.GenerationFailed,
                endedAt,
                out session,
                out sessionError)
            : sessionService.TryCompleteSession(
                session.Id,
                AutonomousPrivateChatSessionEndReason.NaturalConclusion,
                endedAt,
                out session,
                out sessionError);
        Assert.True(ended, sessionError);
        return Assert.IsType<AutonomousPrivateChatSession>(session);
    }

    private AutonomousPrivateChatSession CreateRunningSession(
        AiAccount initiator,
        AiAccount recipient,
        DateTime startedAt)
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        PrivateChatService privateChatService = new(factory);
        Assert.True(privateChatService.TryGetOrCreateAiPrivateChat(
            initiator.Id,
            recipient.Id,
            out PrivateChat? privateChat,
            out _,
            out string chatError), chatError);
        AutonomousPrivateChatSessionService sessionService = new(factory);
        Assert.True(sessionService.TryStartSession(
            privateChat!.Id,
            initiator.Id,
            recipient.Id,
            "进行中的交流",
            maximumRounds: 3,
            continuationRatePercent: 80,
            startedAt,
            out AutonomousPrivateChatSession? session,
            out string sessionError), sessionError);
        return Assert.IsType<AutonomousPrivateChatSession>(session);
    }

    private (AiAccount First, AiAccount Second) CreatePair(string prefix)
    {
        return (
            CreateAccount($"{prefix}First"),
            CreateAccount($"{prefix}Second"));
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

    private AiMemoryService CreateService()
    {
        return new AiMemoryService(_database.CreateDbContextFactory());
    }

    private static AiMemory CreateMemory(
        AiMemoryService service,
        AiAccount owner,
        AiAccount subject,
        AutonomousPrivateChatSession session,
        AiMemoryType type,
        string summary,
        int salience,
        DateTime occurredAt)
    {
        AiMemoryOperationStatus status = service.TryCreateMemory(
            owner.Id,
            subject.Id,
            type,
            summary,
            salience,
            session.PrivateChatId,
            session.Id,
            occurredAt,
            out AiMemory? memory,
            out string errorMessage);
        Assert.Equal(AiMemoryOperationStatus.Success, status);
        Assert.Equal(string.Empty, errorMessage);
        return Assert.IsType<AiMemory>(memory);
    }

    private static void AssertCreateStatus(
        AiMemoryOperationStatus expectedStatus,
        AiMemoryService service,
        Guid ownerAiAccountId,
        Guid subjectAiAccountId,
        AiMemoryType type,
        string summary,
        int salience,
        AutonomousPrivateChatSession session)
    {
        Assert.Equal(
            expectedStatus,
            service.TryCreateMemory(
                ownerAiAccountId,
                subjectAiAccountId,
                type,
                summary,
                salience,
                session.PrivateChatId,
                session.Id,
                session.EndedAt ?? session.LastActivityAt,
                out _,
                out _));
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

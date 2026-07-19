using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证 Session 洞察经过业务映射后对关系和方向记忆产生的实际结果。
/// </summary>
public sealed class SessionPostProcessingServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task ProcessAsync_AppliesDirectionalSignalsAndSavesMemories()
    {
        (AiAccount first, AiAccount second) = CreatePair("Semantic");
        AutonomousPrivateChatSession session = CreateCompletedSession(
            first,
            second,
            new DateTime(2026, 7, 19, 23, 0, 0));
        IReadOnlyList<PrivateMessage> messages = GetMessages(session.Id);
        Guid firstMessageId = messages[0].Id;
        Guid secondMessageId = messages[1].Id;
        StaticInsightAnalyzer analyzer = new(request =>
            new SessionInsightAnalysis(
                new DirectionalSessionInsight(
                    RelationshipSignalPolarity.Positive,
                    RelationshipSignalStrength.High,
                    RelationshipSignalPolarity.Positive,
                    RelationshipSignalStrength.Medium,
                    "对方坦率分享了自己的偏好",
                    new[] { secondMessageId },
                    new SessionMemoryCandidate[]
                    {
                        new(
                            AiMemoryType.Preference,
                            "对方喜欢雨天散步",
                            SessionMemoryImportance.High,
                            new[] { secondMessageId }),
                        new(
                            AiMemoryType.Habit,
                            "对方偶尔散步",
                            SessionMemoryImportance.Low,
                            new[] { secondMessageId }),
                        new(
                            AiMemoryType.PersonalFact,
                            "没有证据的事实",
                            SessionMemoryImportance.High,
                            new[] { Guid.NewGuid() })
                    }),
                new DirectionalSessionInsight(
                    RelationshipSignalPolarity.Negative,
                    RelationshipSignalStrength.Low,
                    RelationshipSignalPolarity.Negative,
                    RelationshipSignalStrength.High,
                    "对方最初否定了自己的偏好",
                    new[] { firstMessageId },
                    new SessionMemoryCandidate[]
                    {
                        new(
                            AiMemoryType.SharedExperience,
                            "双方讨论过雨天安排",
                            SessionMemoryImportance.Medium,
                            new[] { firstMessageId, secondMessageId })
                    }),
                false,
                string.Empty));
        SessionPostProcessingService service = CreateService(analyzer);

        SessionPostProcessingResult result = await service.ProcessAsync(
            session.Id);

        Assert.Equal(SessionPostProcessingStatus.Success, result.Status);
        Assert.Equal(2, result.RelationshipChanges.Count);
        Assert.Equal(2, result.Memories.Count);
        Assert.Equal(1, analyzer.CallCount);

        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        relationshipService.TryGetRelationship(
            first.Id,
            second.Id,
            out AiRelationship? firstToSecond);
        relationshipService.TryGetRelationship(
            second.Id,
            first.Id,
            out AiRelationship? secondToFirst);
        AssertRelationship(firstToSecond, familiarity: 11, affinity: 3, trust: 12);
        AssertRelationship(secondToFirst, familiarity: 11, affinity: -1, trust: 8);

        Assert.Equal(
            AiMemoryOperationStatus.Success,
            new AiMemoryService(_database.CreateDbContextFactory())
                .TryGetActiveMemories(
                    first.Id,
                    second.Id,
                    10,
                    type: null,
                    out IReadOnlyList<AiMemory> firstMemories,
                    out _));
        AiMemory preference = Assert.Single(firstMemories);
        Assert.Equal("对方喜欢雨天散步", preference.Summary);
        Assert.Equal(90, preference.Salience);

        SessionPostProcessingResult repeated = await service.ProcessAsync(
            session.Id);
        Assert.Equal(
            SessionPostProcessingStatus.AlreadyProcessed,
            repeated.Status);
        Assert.Equal(1, analyzer.CallCount);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            2,
            dbContext.AiRelationshipChanges.Count(change =>
                change.SessionId == session.Id));
        Assert.Equal(
            2,
            dbContext.AiMemories.Count(memory =>
                memory.SourceSessionId == session.Id));
    }

    [Fact]
    public async Task ProcessAsync_WhenAnalyzerThrows_UsesSafeFallback()
    {
        (AiAccount first, AiAccount second) = CreatePair("Fallback");
        AutonomousPrivateChatSession session = CreateCompletedSession(
            first,
            second,
            new DateTime(2026, 7, 20, 0, 0, 0));

        SessionPostProcessingResult result = await CreateService(
                new ThrowingInsightAnalyzer())
            .ProcessAsync(session.Id);

        Assert.Equal(
            SessionPostProcessingStatus.SuccessWithFallback,
            result.Status);
        Assert.True(result.Analysis!.UsedFallback);
        Assert.Empty(result.Memories);

        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        relationshipService.TryGetRelationship(
            first.Id,
            second.Id,
            out AiRelationship? relationship);
        AssertRelationship(relationship, familiarity: 11, affinity: 0, trust: 10);
        Assert.All(
            result.RelationshipChanges,
            change => Assert.Contains("基础关系变化", change.Reason));
    }

    [Fact]
    public async Task ProcessAsync_InvalidMemoryCandidate_ReturnsPartialFailure()
    {
        (AiAccount first, AiAccount second) = CreatePair("Partial");
        AutonomousPrivateChatSession session = CreateCompletedSession(
            first,
            second,
            new DateTime(2026, 7, 20, 1, 0, 0));
        Guid subjectMessageId = GetMessages(session.Id)[1].Id;
        StaticInsightAnalyzer analyzer = new(request =>
            new SessionInsightAnalysis(
                new DirectionalSessionInsight(
                    RelationshipSignalPolarity.Positive,
                    RelationshipSignalStrength.High,
                    RelationshipSignalPolarity.Neutral,
                    RelationshipSignalStrength.None,
                    "缺少关系证据",
                    Array.Empty<Guid>(),
                    new SessionMemoryCandidate[]
                    {
                        new(
                            (AiMemoryType)99,
                            "无效类型",
                            SessionMemoryImportance.High,
                            new[] { subjectMessageId })
                    }),
                DirectionalSessionInsight.Neutral("没有明确关系变化"),
                false,
                string.Empty));

        SessionPostProcessingResult result = await CreateService(analyzer)
            .ProcessAsync(session.Id);

        Assert.Equal(
            SessionPostProcessingStatus.MemoryPersistencePartialFailure,
            result.Status);
        Assert.Equal(2, result.RelationshipChanges.Count);
        Assert.Empty(result.Memories);
        Assert.Contains("记忆类型无效", result.Message);

        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        relationshipService.TryGetRelationship(
            first.Id,
            second.Id,
            out AiRelationship? relationship);
        AssertRelationship(relationship, familiarity: 11, affinity: 0, trust: 10);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            2,
            dbContext.AiRelationshipChanges.Count(change =>
                change.SessionId == session.Id));
        Assert.Empty(dbContext.AiMemories);
    }

    private AutonomousPrivateChatSession CreateCompletedSession(
        AiAccount initiator,
        AiAccount recipient,
        DateTime endedAt)
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
            "雨天安排",
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
            AutonomousPrivateChatMessageMode.Single,
            initiatorMessageCount: 1,
            recipientMessageCount: 1,
            endedAt.AddMinutes(-2),
            out AutonomousPrivateChatRound? round,
            out string roundError), roundError);
        Assert.True(sessionService.TryAppendMessage(
            round!.Id,
            initiator,
            "我不太理解为什么有人喜欢下雨天",
            endedAt.AddMinutes(-1),
            out _,
            out _,
            out string firstMessageError), firstMessageError);
        Assert.True(sessionService.TryAppendMessage(
            round.Id,
            recipient,
            "我喜欢雨天散步，因为街上会安静很多",
            endedAt.AddSeconds(-45),
            out _,
            out _,
            out string secondMessageError), secondMessageError);
        Assert.True(sessionService.TryCompleteRound(
            round.Id,
            endedAt.AddSeconds(-30),
            out _,
            out string roundCompletionError), roundCompletionError);
        Assert.True(sessionService.TryCompleteSession(
            session.Id,
            AutonomousPrivateChatSessionEndReason.NaturalConclusion,
            endedAt,
            out session,
            out string completionError), completionError);
        return Assert.IsType<AutonomousPrivateChatSession>(session);
    }

    private IReadOnlyList<PrivateMessage> GetMessages(Guid sessionId)
    {
        return new AutonomousPrivateChatSessionService(
            _database.CreateDbContextFactory()).GetMessages(sessionId);
    }

    private SessionPostProcessingService CreateService(
        ISessionInsightAnalyzer analyzer)
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        return new SessionPostProcessingService(
            new AutonomousPrivateChatSessionService(factory),
            new AiAccountService(factory),
            analyzer,
            new RelationshipEvolutionService(factory),
            new AiMemoryService(factory));
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

    private static void AssertRelationship(
        AiRelationship? relationship,
        int familiarity,
        int affinity,
        int trust)
    {
        Assert.NotNull(relationship);
        Assert.Equal(familiarity, relationship.Familiarity);
        Assert.Equal(affinity, relationship.Affinity);
        Assert.Equal(trust, relationship.Trust);
        Assert.Equal(1, relationship.InteractionCount);
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private sealed class StaticInsightAnalyzer : ISessionInsightAnalyzer
    {
        private readonly Func<
            SessionInsightAnalysisRequest,
            SessionInsightAnalysis> _createAnalysis;

        public int CallCount { get; private set; }

        public StaticInsightAnalyzer(
            Func<SessionInsightAnalysisRequest, SessionInsightAnalysis>
                createAnalysis)
        {
            _createAnalysis = createAnalysis;
        }

        public Task<SessionInsightAnalysis> AnalyzeAsync(
            SessionInsightAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_createAnalysis(request));
        }
    }

    private sealed class ThrowingInsightAnalyzer : ISessionInsightAnalyzer
    {
        public Task<SessionInsightAnalysis> AnalyzeAsync(
            SessionInsightAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new AiMessageGenerationException("测试模型不可用。");
        }
    }
}

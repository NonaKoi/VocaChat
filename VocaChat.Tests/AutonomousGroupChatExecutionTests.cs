using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证自主好友群聊会复用长期群聊，并让至少三位不同好友留下可持久化消息。
/// </summary>
public sealed class AutonomousGroupChatExecutionTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task ExecuteAsync_WithApprovedGroup_PersistsThreeSpeakersAndReusesGroupChat()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup();
        RecordingAiMessageGenerator generator = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(generator);
        DateTime firstStartedAt = new(2026, 7, 19, 15, 0, 0);

        AutonomousGroupChatExecutionResult firstResult =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                firstStartedAt,
                randomJitter: 10,
                requestedTopic: "周末去哪里");

        Assert.Equal(
            AutonomousGroupChatExecutionStatus.Completed,
            firstResult.Status);
        Assert.True(firstResult.GroupChatCreated);
        Assert.NotNull(firstResult.GroupChat);
        Assert.False(firstResult.GroupChat.IncludesLocalUser);
        Assert.NotNull(firstResult.Session);
        Assert.Equal(
            AutonomousGroupChatSessionStatus.Completed,
            firstResult.Session.Status);
        Assert.Equal(3, firstResult.Session.Participants.Count);
        Assert.Equal(4, firstResult.Session.MaximumRounds);
        Assert.Equal(80, firstResult.Session.ContinuationRatePercent);
        Assert.True(generator.Requests.Count >= 3);
        Assert.Equal(
            3,
            generator.Requests.Take(3)
                .Select(request => request.Speaker.Id)
                .Distinct()
                .Count());
        Assert.True(firstResult.Messages.Count >= 3);
        Assert.Equal(
            3,
            firstResult.Messages
                .Select(message => message.SenderAiAccountId)
                .Distinct()
                .Count());
        Assert.All(firstResult.Messages, message =>
            Assert.Equal(
                firstResult.Session.Id,
                message.AutonomousGroupChatSessionId));
        Assert.All(firstResult.Messages, message =>
            Assert.NotNull(message.AutonomousGroupChatRoundId));
        IReadOnlyList<AutonomousGroupChatRound> normalRounds =
            firstResult.Rounds.Where(round => !round.IsClosing).ToList();
        AutonomousGroupChatRound closingRound = Assert.Single(
            firstResult.Rounds,
            round => round.IsClosing);
        Assert.Equal(
            firstResult.Session.CompletedRounds,
            normalRounds.Count);
        Assert.NotNull(closingRound.CompletedAt);
        Assert.All(firstResult.Rounds, round =>
            Assert.NotNull(round.CompletedAt));
        Assert.True(normalRounds.Count >= 1);
        Assert.Equal(1, normalRounds[0].OccurrenceProbability);
        Assert.True(normalRounds
            .Zip(normalRounds.Skip(1), (previous, next) =>
                next.OccurrenceProbability < previous.OccurrenceProbability)
            .All(result => result));
        Assert.All(generator.Requests, request =>
        {
            Assert.Equal(
                AiMessageGenerationScenario.AutonomousGroupChat,
                request.Scenario);
            Assert.Equal(2, request.OtherParticipants.Count);
        });

        AutonomousGroupChatSession? storedSession =
            new AutonomousGroupChatSessionService(
                _database.CreateDbContextFactory())
                .FindById(firstResult.Session.Id);
        GroupChat? storedGroup = new GroupChatService(
            _database.CreateDbContextFactory())
            .FindById(firstResult.GroupChat.Id);
        IReadOnlyList<GroupMessage> storedMessages = new GroupMessageService(
            _database.CreateDbContextFactory())
            .GetOrderedChatHistory(storedGroup!);
        Assert.NotNull(storedSession);
        Assert.Equal(3, storedSession.Participants.Count);
        IReadOnlyList<AutonomousGroupChatRound> storedRounds =
            new AutonomousGroupChatSessionService(
                _database.CreateDbContextFactory())
                .GetRounds(firstResult.Session.Id);
        Assert.Equal(
            firstResult.Rounds.Select(round => round.Id),
            storedRounds.Select(round => round.Id));
        Assert.Equal(
            firstResult.Messages.Select(message => message.Id),
            storedMessages.Select(message => message.Id));

        AutonomousGroupChatExecutionResult secondResult =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                firstStartedAt.AddHours(1),
                randomJitter: 10,
                requestedTopic: "再聊一次");

        Assert.Equal(
            AutonomousGroupChatExecutionStatus.Completed,
            secondResult.Status);
        Assert.False(secondResult.GroupChatCreated);
        Assert.Equal(firstResult.GroupChat.Id, secondResult.GroupChat!.Id);
        Assert.NotEqual(firstResult.Session.Id, secondResult.Session!.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithOneRoundLimit_CompletesOneRoundAndOneClosure()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup(
            groupChatMaximumRounds: 1);
        RecordingAiMessageGenerator generator = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(generator, new ZeroGroupRandomSource());

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 20, 9, 0, 0),
                randomJitter: 10,
                requestedTopic: "硬上限测试");

        Assert.Equal(AutonomousGroupChatExecutionStatus.Completed, result.Status);
        Assert.Equal(
            AutonomousGroupChatSessionEndReason.HardLimitReached,
            result.Session!.EndReason);
        Assert.Equal(1, result.Session.CompletedRounds);
        Assert.Single(result.Rounds, round => !round.IsClosing);
        Assert.Single(result.Rounds, round => round.IsClosing);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLaterGenerationFails_PreservesEarlierMessages()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup();
        FailAfterRequestsGenerator generator = new(successfulRequestCount: 3);
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(generator, new ZeroGroupRandomSource());

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 20, 10, 0, 0),
                randomJitter: 10,
                requestedTopic: "部分失败测试");

        Assert.Equal(
            AutonomousGroupChatExecutionStatus.GenerationFailed,
            result.Status);
        Assert.Equal(
            AutonomousGroupChatSessionStatus.Failed,
            result.Session!.Status);
        Assert.Equal(
            AutonomousGroupChatSessionEndReason.GenerationFailed,
            result.Session.EndReason);
        Assert.NotEmpty(result.Messages);
        Assert.Equal(1, result.Session.CompletedRounds);
        Assert.Equal(2, result.Rounds.Count);

        IReadOnlyList<GroupMessage> storedMessages = new GroupMessageService(
            _database.CreateDbContextFactory())
            .GetOrderedChatHistory(result.GroupChat!);
        Assert.Equal(
            result.Messages.Select(message => message.Id),
            storedMessages.Select(message => message.Id));
    }

    [Fact]
    public void ContinuationDecider_ProducesStrictlyDecreasingProbability()
    {
        AutonomousGroupChatContinuationDecider decider = new();
        AutonomousGroupChatPlan plan = new()
        {
            MemberAiAccountIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
            InitiatorAiAccountId = Guid.NewGuid(),
            ContinuationRatePercent = 95,
            MaximumRounds = 6,
            Decision = new AutonomousGroupChatDecision
            {
                AverageRelationshipScore = 100,
                WeakestRelationshipScore = 100
            }
        };
        AutonomousGroupChatRoundPlan round = new()
        {
            Speakers = new[]
            {
                new AutonomousGroupChatSpeakerPlan
                {
                    SpeakerAiAccountId = Guid.NewGuid(),
                    MessageCount = 1
                },
                new AutonomousGroupChatSpeakerPlan
                {
                    SpeakerAiAccountId = Guid.NewGuid(),
                    MessageCount = 1
                },
                new AutonomousGroupChatSpeakerPlan
                {
                    SpeakerAiAccountId = Guid.NewGuid(),
                    MessageCount = 1
                }
            }
        };

        double previousProbability = 1;
        for (int index = 0; index < 5; index++)
        {
            AutonomousGroupChatContinuationDecision decision = decider.Decide(
                plan,
                previousProbability,
                round,
                previousRoundNaturallyClosed: false,
                randomRoll: 0);

            Assert.True(decision.OccurrenceProbability < previousProbability);
            previousProbability = decision.OccurrenceProbability;
        }
    }

    private IReadOnlyList<AiAccount> CreateStrongGroup(
        int groupChatMaximumRounds = 4)
    {
        AiAccountService accountService = new(
            _database.CreateDbContextFactory());
        List<AiAccount> accounts = new();
        for (int index = 1; index <= 3; index++)
        {
            Assert.True(accountService.TryCreateAiAccount(
                new AiAccountCreationData
                {
                    Nickname = $"ExecutionFriend{index}",
                    InterestTags = new[] { "摄影" }
                },
                out AiAccount? account,
                out string errorMessage), errorMessage);
            accounts.Add(account!);
        }

        AutonomousInteractionSettingsService settingsService = new(
            _database.CreateDbContextFactory());
        Assert.True(settingsService.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.High,
            allowPrivateChats: true,
            allowGroupChats: true,
            privateChatContinuationRatePercent: 80,
            privateChatMaximumRounds: 6,
            autonomousGroupChatMaximumMembers: 6,
            groupChatContinuationRatePercent: 80,
            groupChatMaximumRounds,
            out _,
            out string settingsError), settingsError);

        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        foreach (AiAccount from in accounts)
        {
            foreach (AiAccount to in accounts.Where(account => account.Id != from.Id))
            {
                Assert.Equal(
                    AiRelationshipOperationStatus.Success,
                    relationshipService.TryUpdateRelationship(
                        from.Id,
                        to.Id,
                        familiarity: 100,
                        affinity: 100,
                        trust: 100,
                        out _));
            }
        }

        return accounts.AsReadOnly();
    }

    private AutonomousGroupChatExecutionService CreateExecutionService(
        IAiMessageGenerator generator,
        AutonomousGroupChatRandomSource? randomSource = null)
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        ConversationActionPlanner actionPlanner = new(new Random(7));
        AutonomousGroupChatSpeakerPlanner speakerPlanner = new(factory);
        return new AutonomousGroupChatExecutionService(
            new AutonomousGroupChatJudge(factory),
            new AutonomousGroupChatPlanningService(factory),
            new AutonomousGroupChatRoundPlanner(speakerPlanner),
            new AutonomousGroupChatContinuationDecider(),
            new AutonomousGroupChatClosurePlanner(speakerPlanner),
            randomSource ?? new AutonomousGroupChatRandomSource(new Random(7)),
            new AutonomousGroupChatSessionService(factory),
            new AiAccountService(factory),
            new GroupChatService(factory),
            new GroupMessageService(factory),
            new RuleBasedConversationDirector(actionPlanner),
            generator,
            new AiReplyTimingScheduler(
                factory,
                (_, _) => Task.CompletedTask),
            new ConversationQuestionPolicyService(factory),
            new AiIdentityContinuityService(
                new AiSelfMemoryService(factory),
                new AiInteractionDiagnosticLogService(factory)));
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private sealed class ZeroGroupRandomSource : AutonomousGroupChatRandomSource
    {
        public override double NextUnit() => 0;
    }

    private sealed class FailAfterRequestsGenerator : IAiMessageGenerator
    {
        private readonly int _successfulRequestCount;
        private int _requestCount;

        public FailAfterRequestsGenerator(int successfulRequestCount)
        {
            _successfulRequestCount = successfulRequestCount;
        }

        public Task<IReadOnlyList<string>> GenerateMessagesAsync(
            AiMessageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _requestCount++;
            if (_requestCount > _successfulRequestCount)
            {
                throw new InvalidOperationException("受控生成失败。");
            }

            IReadOnlyList<string> messages = Enumerable
                .Range(1, request.ExpectedMessageCount)
                .Select(index => $"{request.Speaker.Nickname}-{_requestCount}-{index}")
                .ToList()
                .AsReadOnly();
            return Task.FromResult(messages);
        }
    }
}

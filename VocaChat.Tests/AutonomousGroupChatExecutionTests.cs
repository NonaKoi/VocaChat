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
            Assert.NotNull(request.GroupWorldConversationContext);
            Assert.All(
                request.GroupWorldConversationContext!
                    .ParticipantContexts
                    .SelectMany(context => context.RelevantKnowledge),
                knowledge => Assert.Equal(
                    request.Speaker.Id,
                    knowledge.OwnerAiAccountId));
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
        using (VocaChat.Data.VocaChatDbContext audienceDbContext =
               _database.CreateDbContextFactory().CreateDbContext())
        {
            Assert.All(
                firstResult.Messages,
                message => Assert.Equal(
                    3,
                    audienceDbContext.GroupMessageAudience.Count(audience =>
                        audience.GroupMessageId == message.Id)));
        }

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
    public async Task ExecuteAsync_WhenFirstSpeakerFails_CompletesRoundWithRemainingSpeakers()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup(
            groupChatMaximumRounds: 1);
        FailFirstRequestGenerator generator = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(generator, new ZeroGroupRandomSource());

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 20, 9, 30, 0),
                randomJitter: 10,
                requestedTopic: "单成员失败后继续测试");

        Assert.Equal(
            AutonomousGroupChatExecutionStatus.Completed,
            result.Status);
        AutonomousGroupChatRound normalRound = Assert.Single(
            result.Rounds,
            round => !round.IsClosing);
        IReadOnlyList<GroupMessage> normalRoundMessages = result.Messages
            .Where(message =>
                message.AutonomousGroupChatRoundId == normalRound.Id)
            .ToList()
            .AsReadOnly();
        Assert.NotEmpty(normalRoundMessages);
        Guid failedSpeakerId = generator.Requests[0].Speaker.Id;
        Assert.DoesNotContain(
            normalRoundMessages,
            message => message.SenderAiAccountId == failedSpeakerId);
        Assert.Equal(
            normalRoundMessages
                .Select(message => message.SenderAiAccountId)
                .Distinct()
                .Count(),
            normalRound.PlannedSpeakerCount);
        Assert.Equal(
            normalRoundMessages.Count,
            normalRound.PlannedMessageCount);

        AiInteractionDiagnosticLogService diagnosticService = new(
            _database.CreateDbContextFactory());
        AiInteractionDiagnosticLog failureLog = Assert.Single(
            diagnosticService.GetRecent(),
            log => log.Code ==
                AiInteractionDiagnosticCode.GroupConversationExecutionFailed);
        Assert.Equal(failedSpeakerId, failureLog.AiAccountId);
        Assert.True(failureLog.WasRecovered);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllSpeakersFail_MarksSessionAsGenerationFailed()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup(
            groupChatMaximumRounds: 1);
        FailAfterRequestsGenerator generator = new(successfulRequestCount: 0);
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(generator, new ZeroGroupRandomSource());

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 20, 9, 45, 0),
                randomJitter: 10,
                requestedTopic: "全部成员失败测试");

        Assert.Equal(
            AutonomousGroupChatExecutionStatus.GenerationFailed,
            result.Status);
        Assert.Equal(
            AutonomousGroupChatSessionStatus.Failed,
            result.Session!.Status);
        Assert.Equal(
            AutonomousGroupChatSessionEndReason.GenerationFailed,
            result.Session.EndReason);
        Assert.Empty(result.Messages);

        IReadOnlyList<AiInteractionDiagnosticLog> failureLogs =
            new AiInteractionDiagnosticLogService(
                    _database.CreateDbContextFactory())
                .GetRecent()
                .Where(log => log.Code ==
                    AiInteractionDiagnosticCode
                        .GroupConversationExecutionFailed)
                .ToList()
                .AsReadOnly();
        Assert.NotEmpty(failureLogs);
        Assert.All(failureLogs, log => Assert.False(log.WasRecovered));
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

    [Fact]
    public void ContinuationDecider_LowInformationReducesGroupProbability()
    {
        AutonomousGroupChatContinuationDecider decider = new();
        AutonomousGroupChatPlan plan = new()
        {
            MemberAiAccountIds = new[]
            {
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid()
            },
            InitiatorAiAccountId = Guid.NewGuid(),
            ContinuationRatePercent = 80,
            MaximumRounds = 6,
            Decision = new AutonomousGroupChatDecision
            {
                AverageRelationshipScore = 70,
                WeakestRelationshipScore = 60
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
                }
            }
        };

        double normalProbability = decider.Decide(
            plan,
            1,
            round,
            previousRoundNaturallyClosed: false,
            randomRoll: 0.99).OccurrenceProbability;
        double lowInformationProbability = decider.Decide(
            plan,
            1,
            round,
            previousRoundNaturallyClosed: false,
            randomRoll: 0.99,
            consecutiveLowInformationRounds: 2).OccurrenceProbability;

        Assert.True(lowInformationProbability < normalProbability);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSemanticOrderAndPersistsActualReplyTarget()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup(
            groupChatMaximumRounds: 1);
        RecordingAiMessageGenerator generator = new();
        ScriptedAutonomousGroupDirector groupDirector = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(
                generator,
                new ZeroGroupRandomSource(),
                groupDirector);

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 22, 8, 0, 0),
                randomJitter: 10,
                requestedTopic: "单元 D 语义目标测试");

        Assert.Equal(AutonomousGroupChatExecutionStatus.Completed, result.Status);
        Assert.Equal(
            new[]
            {
                GroupConversationPlanningScenario.AutonomousOpening,
                GroupConversationPlanningScenario.AutonomousClosing
            },
            groupDirector.Requests.Select(request => request.Scenario));
        GroupConversationPlanningRequest openingRequest =
            groupDirector.Requests[0];
        Assert.Null(openingRequest.AnchorMessage);
        Assert.Equal(
            openingRequest.RequiredSpeakerAiAccountId,
            generator.Requests[0].Speaker.Id);
        Assert.All(groupDirector.ReturnedPlans.SelectMany(plan => plan.Speakers),
            speakerPlan => Assert.NotEqual(
                GroupConversationAudience.LocalUser,
                speakerPlan.Audience));

        AiMessageGenerationRequest respondingRequest = generator.Requests
            .First(request => request.Speaker.Id !=
                openingRequest.RequiredSpeakerAiAccountId);
        Assert.NotNull(respondingRequest.ReplyTarget?.Message);
        Assert.Equal(
            openingRequest.RequiredSpeakerAiAccountId,
            respondingRequest.ReplyTarget!.Message!.SenderAiAccountId);
        Assert.Equal(
            openingRequest.RequiredSpeakerAiAccountId,
            respondingRequest.RelationshipTarget?.Id);
        Assert.NotNull(respondingRequest.SpeakerToOtherRelationshipScore);
        Assert.NotNull(respondingRequest.OtherToSpeakerRelationshipScore);
        GroupMessage lastInitiatorMessage = result.Messages.Last(message =>
            message.SenderAiAccountId ==
                openingRequest.RequiredSpeakerAiAccountId);
        Assert.All(
            result.Messages.Where(message =>
                message.SenderAiAccountId == respondingRequest.Speaker.Id),
            message => Assert.Equal(
                lastInitiatorMessage.Id,
                message.ReplyToMessageId));
        Assert.All(result.Messages, message =>
            Assert.NotNull(message.InteractionBatchId));
    }

    [Fact]
    public async Task ExecuteAsync_WithFourMessagesPerSpeaker_PersistsTwelveMessageRound()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup(
            groupChatMaximumRounds: 1,
            minimumReplyMessageCount: 4,
            maximumReplyMessageCount: 4,
            groupChatMaximumMessagesPerTurn: 12);
        RecordingAiMessageGenerator generator = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(
                generator,
                new ZeroGroupRandomSource());

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 22, 9, 0, 0),
                randomJitter: 10,
                requestedTopic: "单轮十二条消息约束测试");

        Assert.Equal(AutonomousGroupChatExecutionStatus.Completed, result.Status);
        AutonomousGroupChatRound normalRound = Assert.Single(
            result.Rounds,
            round => !round.IsClosing);
        Assert.Equal(3, normalRound.PlannedSpeakerCount);
        Assert.Equal(12, normalRound.PlannedMessageCount);
        Assert.Equal(
            12,
            result.Messages.Count(message =>
                message.AutonomousGroupChatRoundId == normalRound.Id));
        Assert.All(generator.Requests.Take(3), request =>
            Assert.Equal(4, request.ExpectedMessageCount));
    }

    [Fact]
    public async Task ExecuteAsync_UsesConfiguredWholeGroupSpeakerLimit()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup(
            groupChatMaximumRounds: 1,
            groupChatWholeGroupMaximumSpeakersPerTurn: 2,
            groupChatMaximumMessagesPerTurn: 4);
        ScriptedAutonomousGroupDirector groupDirector = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(
                new RecordingAiMessageGenerator(),
                new ZeroGroupRandomSource(),
                groupDirector);

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 22, 9, 30, 0),
                randomJitter: 10,
                requestedTopic: "群聊密度设置测试");

        Assert.Equal(AutonomousGroupChatExecutionStatus.Completed, result.Status);
        GroupConversationPlanningRequest openingRequest =
            groupDirector.Requests.First(request => request.Scenario ==
                GroupConversationPlanningScenario.AutonomousOpening);
        Assert.Equal(2, openingRequest.MaximumSpeakerCount);
        Assert.Equal(4, openingRequest.MaximumTotalMessageCount);
        Assert.All(result.Rounds, round =>
        {
            Assert.True(round.PlannedSpeakerCount <= 2);
            Assert.True(round.PlannedMessageCount <= 4);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WhenProbabilityDeclines_DoesNotPlanContinuationRound()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup();
        ScriptedAutonomousGroupDirector groupDirector = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(
                new RecordingAiMessageGenerator(),
                new HighGroupRandomSource(),
                groupDirector);

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 22, 10, 0, 0),
                randomJitter: 10,
                requestedTopic: "概率先于语义规划测试");

        Assert.Equal(AutonomousGroupChatExecutionStatus.Completed, result.Status);
        Assert.Equal(
            AutonomousGroupChatSessionEndReason
                .ContinuationProbabilityDeclined,
            result.Session!.EndReason);
        Assert.DoesNotContain(
            groupDirector.Requests,
            request => request.Scenario ==
                GroupConversationPlanningScenario.AutonomousContinuation);
        Assert.Equal(2, groupDirector.Requests.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSemanticPlanIsInvalid_UsesSafeRuleFallback()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup(
            groupChatMaximumRounds: 1);
        RecordingAiMessageGenerator generator = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(
                generator,
                new ZeroGroupRandomSource(),
                new InvalidAutonomousGroupDirector());

        AutonomousGroupChatExecutionResult result =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                new DateTime(2026, 7, 22, 11, 0, 0),
                randomJitter: 10,
                requestedTopic: "无效导演回退测试");

        Assert.Equal(AutonomousGroupChatExecutionStatus.Completed, result.Status);
        Assert.NotEmpty(generator.Requests);
        Assert.Equal(
            result.Session!.InitiatorAiAccountId,
            generator.Requests[0].Speaker.Id);
        Assert.All(generator.Requests, request =>
        {
            Assert.NotNull(request.GroupConversationPlan);
            Assert.NotEqual(
                GroupConversationAudience.LocalUser,
                request.GroupConversationPlan!.Audience);
        });
    }

    private IReadOnlyList<AiAccount> CreateStrongGroup(
        int groupChatMaximumRounds = 4,
        int minimumReplyMessageCount = 1,
        int maximumReplyMessageCount = 4,
        int groupChatWholeGroupMaximumSpeakersPerTurn = 3,
        int groupChatMaximumMessagesPerTurn = 6)
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
            replyDelayMode: AiReplyDelayMode.Fixed,
            fixedReplyDelayMilliseconds: 0,
            minimumReplyDelayMilliseconds: 0,
            maximumReplyDelayMilliseconds: 0,
            consecutiveMessageDelayMode: AiReplyDelayMode.Fixed,
            fixedConsecutiveMessageDelayMilliseconds: 0,
            minimumConsecutiveMessageDelayMilliseconds: 0,
            maximumConsecutiveMessageDelayMilliseconds: 0,
            maximumConsecutiveQuestionTurns: 2,
            minimumReplyMessageCount,
            maximumReplyMessageCount,
            groupChatMaximumSpeakersPerTurn: 2,
            groupChatWholeGroupMaximumSpeakersPerTurn,
            groupChatMaximumMessagesPerTurn,
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
        AutonomousGroupChatRandomSource? randomSource = null,
        IGroupConversationDirector? groupConversationDirector = null)
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        ConversationActionPlanner actionPlanner = new(new Random(7));
        AutonomousGroupChatSpeakerPlanner speakerPlanner = new(factory);
        GroupChatReplyPlanner replyPlanner = new(factory);
        AiIdentityContinuityService identityContinuityService = new(
            new AiSelfMemoryService(factory),
            new AiInteractionDiagnosticLogService(factory));
        GroupConversationContextService conversationContextService = new(
            factory,
            identityContinuityService,
            new AiWorldConversationContextService(
                factory,
                new AiWorldAwarenessService(factory),
                new AiWorldKnowledgeService(factory),
                new AiWorldKnowledgeCandidateExtractor()));
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
            replyPlanner,
            groupConversationDirector
                ?? new RuleBasedGroupConversationDirector(replyPlanner),
            new GroupConversationPlanValidator(),
            new RuleBasedConversationDirector(actionPlanner),
            generator,
            new AiReplyTimingScheduler(
                factory,
                (_, _) => Task.CompletedTask),
            new ConversationQuestionPolicyService(factory),
            identityContinuityService,
            new AiReplyMessageCountSettingsResolver(factory),
            conversationContextService,
            new GroupConversationDensitySettingsResolver(factory),
            new GroupConversationDiagnosticService(
                new AiInteractionDiagnosticLogService(factory)),
            CreateWorldKnowledgeProcessor(factory));
    }

    private static AiWorldKnowledgeMessageProcessor
        CreateWorldKnowledgeProcessor(
            VocaChat.Data.VocaChatDbContextFactory factory)
    {
        return new AiWorldKnowledgeMessageProcessor(
            factory,
            new AiWorldKnowledgeCandidateExtractor(),
            new AiWorldKnowledgeService(factory),
            new AiWorldAwarenessService(factory));
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private sealed class ZeroGroupRandomSource : AutonomousGroupChatRandomSource
    {
        public override double NextUnit() => 0;
    }

    private sealed class HighGroupRandomSource : AutonomousGroupChatRandomSource
    {
        public override double NextUnit() => 0.99;
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

    private sealed class FailFirstRequestGenerator : IAiMessageGenerator
    {
        private bool _failed;

        public List<AiMessageGenerationRequest> Requests { get; } = new();

        public Task<IReadOnlyList<string>> GenerateMessagesAsync(
            AiMessageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (!_failed)
            {
                _failed = true;
                throw new InvalidOperationException(
                    "受控首位成员生成失败。");
            }

            IReadOnlyList<string> messages = Enumerable
                .Range(1, request.ExpectedMessageCount)
                .Select(index =>
                    $"{request.Speaker.Nickname}-局部恢复-{index}")
                .ToList()
                .AsReadOnly();
            return Task.FromResult(messages);
        }
    }


    private sealed class ScriptedAutonomousGroupDirector
        : IGroupConversationDirector
    {
        public List<GroupConversationPlanningRequest> Requests { get; } = new();
        public List<GroupConversationTurnPlan> ReturnedPlans { get; } = new();

        public Task<GroupConversationTurnPlan> CreatePlanAsync(
            GroupConversationPlanningRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);

            GroupConversationTurnPlan plan;
            if (request.Scenario ==
                GroupConversationPlanningScenario.AutonomousClosing)
            {
                plan = new GroupConversationTurnPlan
                {
                    AnchorMessageId = request.AnchorMessage?.Id,
                    TopicFocus = request.Topic,
                    TurnGoal = "已经自然结束，不追加收束消息",
                    Speakers = Array.Empty<GroupConversationSpeakerPlan>(),
                    SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection
                };
            }
            else
            {
                Guid initiatorId = request.RequiredSpeakerAiAccountId!.Value;
                Guid responderId = request.GroupChat.Members
                    .First(member => member.Id != initiatorId)
                    .Id;
                plan = new GroupConversationTurnPlan
                {
                    AnchorMessageId = request.AnchorMessage?.Id,
                    TopicFocus = request.Topic,
                    TurnGoal = "发起者开场后由一位好友作出真实回应",
                    Speakers = new[]
                    {
                        new GroupConversationSpeakerPlan
                        {
                            SpeakerAiAccountId = initiatorId,
                            ReplyTargetMessageId = null,
                            Audience = GroupConversationAudience.WholeGroup,
                            Role = GroupConversationRole.ShiftTopic,
                            ResponseGoal = "自然引入话题",
                            NewContribution = "说出本次话题"
                        },
                        new GroupConversationSpeakerPlan
                        {
                            SpeakerAiAccountId = responderId,
                            ReplyTargetMessageId = null,
                            TargetAiAccountId = initiatorId,
                            Audience =
                                GroupConversationAudience.SpecificAiAccount,
                            Role = GroupConversationRole.React,
                            ResponseGoal = "回应发起者的实际内容",
                            NewContribution = "给出与开场不同的新反应"
                        }
                    },
                    SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection
                };
            }

            ReturnedPlans.Add(plan);
            return Task.FromResult(plan);
        }
    }

    private sealed class InvalidAutonomousGroupDirector
        : IGroupConversationDirector
    {
        public Task<GroupConversationTurnPlan> CreatePlanAsync(
            GroupConversationPlanningRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Guid speakerId = request.RequiredSpeakerAiAccountId
                ?? request.GroupChat.Members[0].Id;
            return Task.FromResult(new GroupConversationTurnPlan
            {
                AnchorMessageId = request.AnchorMessage?.Id,
                TopicFocus = request.Topic,
                TurnGoal = "返回一个越界计划以验证业务回退",
                Speakers = new[]
                {
                    new GroupConversationSpeakerPlan
                    {
                        SpeakerAiAccountId = speakerId,
                        ReplyTargetMessageId = request.AnchorMessage?.Id,
                        Audience = GroupConversationAudience.LocalUser,
                        Role = GroupConversationRole.DirectAnswer,
                        ResponseGoal = "错误地回应本地用户",
                        NewContribution = "无效计划"
                    }
                },
                SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection
            });
        }
    }
}

using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证自主私信完整执行中的概率终止、硬上限、收束和持久化结果。
/// </summary>
public sealed class AutonomousPrivateChatExecutionServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task RejectedDecision_DoesNotCreateChatOrMessages()
    {
        (AiAccount first, AiAccount second) = CreatePair("Rejected");

        AutonomousPrivateChatExecutionResult result = await CreateService().ExecuteAsync(
            first.Id,
            second.Id,
            new DateTime(2026, 7, 18, 10, 0, 0));

        Assert.Equal(
            AutonomousPrivateChatExecutionStatus.DecisionRejected,
            result.Status);
        Assert.Equal(
            AutonomousPrivateChatDecisionStage.GlobalDisabled,
            result.Decision.Stage);
        Assert.Null(result.PrivateChat);
        Assert.Empty(result.Rounds);
        Assert.Empty(result.Messages);
        Assert.Null(new PrivateChatService(_database.CreateDbContextFactory())
            .FindByAiAccountPair(first.Id, second.Id));
    }

    [Fact]
    public async Task ApprovedDecision_StopsByProbabilityAndRunsOneClosure()
    {
        (AiAccount first, AiAccount second) = CreatePair("Probability");
        EnableAutonomousPrivateChats(
            continuationRatePercent: 0,
            maximumRounds: 6);
        AutonomousInteractionSettings storedSettings =
            new AutonomousInteractionSettingsService(
                _database.CreateDbContextFactory()).GetSettings();
        Assert.Equal(0, storedSettings.PrivateChatContinuationRatePercent);
        Assert.Equal(6, storedSettings.PrivateChatMaximumRounds);
        SetStrongRelationships(first.Id, second.Id);
        DateTime occurredAt = new(2026, 7, 18, 11, 0, 0);

        AutonomousPrivateChatExecutionResult result = await CreateService(
            new ConstantRandom(0.5)).ExecuteAsync(
                first.Id,
                second.Id,
                occurredAt,
                "共同爱好");

        Assert.Equal(AutonomousPrivateChatExecutionStatus.Completed, result.Status);
        Assert.True(result.PrivateChatCreated);
        Assert.NotNull(result.PrivateChat);
        Assert.NotNull(result.Session);
        Assert.Equal(AutonomousPrivateChatSessionStatus.Completed, result.Session.Status);
        Assert.Equal(
            AutonomousPrivateChatSessionEndReason.ContinuationProbabilityDeclined,
            result.Session.EndReason);
        Assert.Equal("共同爱好", result.Session.Topic);
        Assert.Equal(6, result.Session.MaximumRounds);
        Assert.Equal(0, result.Session.ContinuationRatePercent);
        Assert.Equal(1, result.Session.CompletedRounds);
        Assert.Equal(2, result.Rounds.Count);
        Assert.False(result.Rounds[0].IsClosing);
        Assert.True(result.Rounds[1].IsClosing);
        Assert.Equal(1, result.Rounds[0].OccurrenceProbability);
        Assert.Null(result.Rounds[0].RandomRoll);
        Assert.All(
            result.Messages,
            message => Assert.Equal(
                result.Session.Id,
                message.AutonomousPrivateChatSessionId));
        Assert.Equal(
            Enumerable.Range(1, result.Messages.Count).Select(value => (int?)value),
            result.Messages.Select(message => message.AutonomousSequenceNumber));

        AutonomousPrivateChatSessionService restartedSessionService = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            result.Messages.Select(message => message.Id),
            restartedSessionService.GetMessages(result.Session.Id)
                .Select(message => message.Id));
        Assert.Equal(2, restartedSessionService.GetRounds(result.Session.Id).Count);

        AssertInteractionsWereRecordedOnce(first.Id, second.Id, occurredAt);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            2,
            dbContext.AiRelationshipChanges.Count(change =>
                change.SessionId == result.Session.Id));
    }

    [Fact]
    public async Task ApprovedDecision_HardMaximumStopsNormalRoundsBeforeClosure()
    {
        (AiAccount first, AiAccount second) = CreatePair("HardLimit");
        EnableAutonomousPrivateChats(
            continuationRatePercent: 95,
            maximumRounds: 2);
        SetStrongRelationships(first.Id, second.Id);

        AutonomousPrivateChatExecutionResult result = await CreateService(
            new ConstantRandom(0)).ExecuteAsync(
                first.Id,
                second.Id,
                new DateTime(2026, 7, 18, 12, 0, 0));

        Assert.Equal(AutonomousPrivateChatExecutionStatus.Completed, result.Status);
        Assert.Equal(
            AutonomousPrivateChatSessionEndReason.HardLimitReached,
            result.Session!.EndReason);
        Assert.Equal(2, result.Session.CompletedRounds);
        Assert.Equal(3, result.Rounds.Count);
        Assert.Equal(2, result.Rounds.Count(round => !round.IsClosing));
        Assert.Single(result.Rounds, round => round.IsClosing);
        Assert.True(result.Rounds[1].OccurrenceProbability < 1);
        Assert.Equal(0, result.Rounds[1].RandomRoll);
    }

    [Fact]
    public async Task ApprovedDecision_AssignsEachNormalTurnAnExplicitTarget()
    {
        (AiAccount first, AiAccount second) = CreatePair("Targets");
        EnableAutonomousPrivateChats(
            continuationRatePercent: 95,
            maximumRounds: 2);
        SetStrongRelationships(first.Id, second.Id);
        RecordingAiMessageGenerator generator = new();

        AutonomousPrivateChatExecutionResult result = await CreateService(
            new ConstantRandom(0.5),
            generator).ExecuteAsync(
                first.Id,
                second.Id,
                new DateTime(2026, 7, 18, 12, 30, 0),
                "周末安排");

        Assert.Equal(AutonomousPrivateChatExecutionStatus.Completed, result.Status);
        IReadOnlyList<AiMessageGenerationRequest> normalRequests = generator
            .Requests
            .Where(request => request.Scenario ==
                AiMessageGenerationScenario.AutonomousPrivateChat)
            .ToList()
            .AsReadOnly();
        Assert.Equal(4, normalRequests.Count);
        Assert.Equal(
            AiDialogueReplyTargetKind.TopicOpening,
            normalRequests[0].ReplyTarget!.Kind);
        Assert.Equal(
            normalRequests[0].Speaker.Id,
            normalRequests[1].ReplyTarget!.Message!.SenderAiAccountId);
        Assert.Equal(
            normalRequests[1].Speaker.Id,
            normalRequests[2].ReplyTarget!.Message!.SenderAiAccountId);
        Assert.Equal(
            normalRequests[2].Speaker.Id,
            normalRequests[3].ReplyTarget!.Message!.SenderAiAccountId);
        Assert.All(
            normalRequests.Skip(1),
            request => Assert.Equal(
                AiDialogueReplyTargetKind.Message,
                request.ReplyTarget!.Kind));
    }

    [Fact]
    public async Task ImmediateSecondExecution_IsRejectedByCooldownWithoutExtraMessages()
    {
        (AiAccount first, AiAccount second) = CreatePair("Cooldown");
        EnableAutonomousPrivateChats(
            continuationRatePercent: 0,
            maximumRounds: 6);
        SetStrongRelationships(first.Id, second.Id);
        DateTime firstRunAt = new(2026, 7, 18, 13, 0, 0);
        AutonomousPrivateChatExecutionService service = CreateService(
            new ConstantRandom(0.5));

        AutonomousPrivateChatExecutionResult firstResult = await service.ExecuteAsync(
            first.Id,
            second.Id,
            firstRunAt);
        int messageCountAfterFirstRun = firstResult.Messages.Count;
        AutonomousPrivateChatExecutionResult secondResult = await service.ExecuteAsync(
            first.Id,
            second.Id,
            firstRunAt.AddMinutes(1));

        Assert.Equal(AutonomousPrivateChatExecutionStatus.Completed, firstResult.Status);
        Assert.Equal(
            AutonomousPrivateChatExecutionStatus.DecisionRejected,
            secondResult.Status);
        Assert.Equal(
            AutonomousPrivateChatDecisionStage.CooldownActive,
            secondResult.Decision.Stage);
        Assert.Equal(
            messageCountAfterFirstRun,
            new PrivateChatService(_database.CreateDbContextFactory())
                .GetOrderedChatHistory(firstResult.PrivateChat!.Id)
                .Count);
    }

    private void AssertInteractionsWereRecordedOnce(
        Guid firstId,
        Guid secondId,
        DateTime occurredAt)
    {
        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryGetRelationship(
                firstId,
                secondId,
                out AiRelationship? firstToSecond));
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryGetRelationship(
                secondId,
                firstId,
                out AiRelationship? secondToFirst));
        Assert.Equal(1, firstToSecond!.InteractionCount);
        Assert.Equal(1, secondToFirst!.InteractionCount);
        Assert.True(firstToSecond.LastInteractionAt >= occurredAt);
        Assert.Equal(firstToSecond.LastInteractionAt, secondToFirst.LastInteractionAt);
    }

    private (AiAccount First, AiAccount Second) CreatePair(string prefix)
    {
        return (CreateAccount($"{prefix}A"), CreateAccount($"{prefix}B"));
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

    private void EnableAutonomousPrivateChats(
        int continuationRatePercent,
        int maximumRounds)
    {
        AutonomousInteractionSettingsService service = new(
            _database.CreateDbContextFactory());
        Assert.True(service.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.Normal,
            allowPrivateChats: true,
            allowGroupChats: false,
            continuationRatePercent,
            maximumRounds,
            out _,
            out string errorMessage), errorMessage);
    }

    private void SetStrongRelationships(Guid firstId, Guid secondId)
    {
        SetStrongRelationship(firstId, secondId);
        SetStrongRelationship(secondId, firstId);
    }

    private void SetStrongRelationship(Guid fromId, Guid toId)
    {
        AiRelationshipService service = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            service.TryUpdateRelationship(
                fromId,
                toId,
                familiarity: 100,
                affinity: 100,
                trust: 100,
                out _));
    }

    private AutonomousPrivateChatExecutionService CreateService(
        Random? random = null,
        IAiMessageGenerator? messageGenerator = null)
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        return new AutonomousPrivateChatExecutionService(
            new AutonomousPrivateChatJudge(factory),
            new AiAccountService(factory),
            new PrivateChatService(factory),
            new AutonomousPrivateChatPlanningService(factory),
            new AutonomousPrivateChatSessionService(factory),
            new AutonomousPrivateChatRoundPlanner(),
            new AutonomousPrivateChatContinuationDecider(),
            new AutonomousPrivateChatClosurePlanner(),
            new AutonomousPrivateChatRandomSource(random ?? new ConstantRandom(0.5)),
            messageGenerator ?? new FakeAiReplyService(),
            new RuleBasedConversationDirector(
                new ConversationActionPlanner(new ConstantRandom(0.5))),
            new RelationshipEvolutionService(factory));
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private sealed class ConstantRandom : Random
    {
        private readonly double _value;

        public ConstantRandom(double value)
        {
            _value = value;
        }

        protected override double Sample()
        {
            return _value;
        }
    }
}

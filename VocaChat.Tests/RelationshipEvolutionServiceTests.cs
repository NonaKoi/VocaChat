using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证自主私信完成后的有界关系变化、双向审计和幂等保护。
/// </summary>
public sealed class RelationshipEvolutionServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void CompletedSession_AppliesAndPersistsBothDirections()
    {
        (AiAccount first, AiAccount second) = CreatePair("Completed");
        DateTime endedAt = new(2026, 7, 19, 13, 0, 0);
        AutonomousPrivateChatSession session = CreateCompletedSession(
            first,
            second,
            endedAt);

        RelationshipEvolutionStatus status = CreateService()
            .TryApplyCompletedSession(
                session.Id,
                out IReadOnlyList<AiRelationshipChange> changes,
                out string errorMessage);

        Assert.Equal(RelationshipEvolutionStatus.Success, status);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(2, changes.Count);
        Assert.All(changes, change =>
        {
            Assert.Equal(session.Id, change.SessionId);
            Assert.Equal(1, change.FamiliarityDelta);
            Assert.Equal(0, change.AffinityDelta);
            Assert.Equal(0, change.TrustDelta);
            Assert.False(string.IsNullOrWhiteSpace(change.Reason));
            Assert.Equal(endedAt, change.CreatedAt);
        });

        AiRelationshipService restartedRelationshipService = new(
            _database.CreateDbContextFactory());
        restartedRelationshipService.TryGetRelationship(
            first.Id,
            second.Id,
            out AiRelationship? firstToSecond);
        restartedRelationshipService.TryGetRelationship(
            second.Id,
            first.Id,
            out AiRelationship? secondToFirst);

        AssertRelationshipProgress(firstToSecond, endedAt);
        AssertRelationshipProgress(secondToFirst, endedAt);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            2,
            dbContext.AiRelationshipChanges.Count(change =>
                change.SessionId == session.Id));
    }

    [Fact]
    public void ApplyingTheSameSessionTwice_DoesNotChangeRelationshipsAgain()
    {
        (AiAccount first, AiAccount second) = CreatePair("Idempotent");
        AutonomousPrivateChatSession session = CreateCompletedSession(
            first,
            second,
            new DateTime(2026, 7, 19, 14, 0, 0));
        RelationshipEvolutionService service = CreateService();

        RelationshipEvolutionStatus firstStatus =
            service.TryApplyCompletedSession(session.Id, out _, out _);
        RelationshipEvolutionStatus secondStatus =
            service.TryApplyCompletedSession(
                session.Id,
                out IReadOnlyList<AiRelationshipChange> existingChanges,
                out string errorMessage);

        Assert.Equal(RelationshipEvolutionStatus.Success, firstStatus);
        Assert.Equal(RelationshipEvolutionStatus.AlreadyApplied, secondStatus);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(2, existingChanges.Count);

        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        relationshipService.TryGetRelationship(
            first.Id,
            second.Id,
            out AiRelationship? relationship);
        Assert.Equal(11, relationship!.Familiarity);
        Assert.Equal(1, relationship.InteractionCount);
    }

    [Fact]
    public void FamiliarityAtMaximum_IsClampedAndAuditsActualDelta()
    {
        (AiAccount first, AiAccount second) = CreatePair("Clamped");
        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryUpdateRelationship(
                first.Id,
                second.Id,
                familiarity: 100,
                affinity: 20,
                trust: 30,
                out _));
        AutonomousPrivateChatSession session = CreateCompletedSession(
            first,
            second,
            new DateTime(2026, 7, 19, 15, 0, 0));

        RelationshipEvolutionStatus status = CreateService()
            .TryApplyCompletedSession(
                session.Id,
                out IReadOnlyList<AiRelationshipChange> changes,
                out _);

        Assert.Equal(RelationshipEvolutionStatus.Success, status);
        AiRelationshipChange firstDirection = Assert.Single(changes, change =>
            change.FromAiAccountId == first.Id);
        Assert.Equal(0, firstDirection.FamiliarityDelta);
        relationshipService.TryGetRelationship(
            first.Id,
            second.Id,
            out AiRelationship? updated);
        Assert.Equal(100, updated!.Familiarity);
        Assert.Equal(20, updated.Affinity);
        Assert.Equal(30, updated.Trust);
        Assert.Equal(1, updated.InteractionCount);
    }

    [Fact]
    public void MissingRunningAndEmptyCompletedSessions_AreRejected()
    {
        Assert.Equal(
            RelationshipEvolutionStatus.SessionNotFound,
            CreateService().TryApplyCompletedSession(
                Guid.NewGuid(),
                out _,
                out _));

        (AiAccount first, AiAccount second) = CreatePair("Rejected");
        AutonomousPrivateChatSession runningSession = StartSession(
            first,
            second,
            new DateTime(2026, 7, 19, 16, 0, 0));
        Assert.Equal(
            RelationshipEvolutionStatus.SessionNotCompleted,
            CreateService().TryApplyCompletedSession(
                runningSession.Id,
                out _,
                out _));

        AutonomousPrivateChatSessionService sessionService = new(
            _database.CreateDbContextFactory());
        Assert.True(sessionService.TryCompleteSession(
            runningSession.Id,
            AutonomousPrivateChatSessionEndReason.NaturalConclusion,
            new DateTime(2026, 7, 19, 16, 5, 0),
            out AutonomousPrivateChatSession? emptyCompletedSession,
            out string completionError), completionError);
        Assert.Equal(
            RelationshipEvolutionStatus.SessionHasNoCompletedRounds,
            CreateService().TryApplyCompletedSession(
                emptyCompletedSession!.Id,
                out _,
                out _));
    }

    private AutonomousPrivateChatSession CreateCompletedSession(
        AiAccount initiator,
        AiAccount recipient,
        DateTime endedAt)
    {
        AutonomousPrivateChatSession session = StartSession(
            initiator,
            recipient,
            endedAt.AddMinutes(-2));
        AutonomousPrivateChatSessionService sessionService = new(
            _database.CreateDbContextFactory());
        Assert.True(sessionService.TryStartRound(
            session.Id,
            isClosing: false,
            occurrenceProbability: 1,
            randomRoll: null,
            AutonomousPrivateChatMessageMode.Single,
            AutonomousPrivateChatMessageMode.None,
            initiatorMessageCount: 1,
            recipientMessageCount: 0,
            endedAt.AddMinutes(-1),
            out AutonomousPrivateChatRound? round,
            out string roundError), roundError);
        Assert.True(sessionService.TryAppendMessage(
            round!.Id,
            initiator,
            "今天过得怎么样",
            endedAt.AddSeconds(-30),
            out _,
            out _,
            out string messageError), messageError);
        Assert.True(sessionService.TryCompleteRound(
            round.Id,
            endedAt.AddSeconds(-20),
            out _,
            out string roundCompletionError), roundCompletionError);
        Assert.True(sessionService.TryCompleteSession(
            session.Id,
            AutonomousPrivateChatSessionEndReason.NaturalConclusion,
            endedAt,
            out AutonomousPrivateChatSession? completedSession,
            out string completionError), completionError);
        return Assert.IsType<AutonomousPrivateChatSession>(completedSession);
    }

    private AutonomousPrivateChatSession StartSession(
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
            "近况",
            maximumRounds: 3,
            continuationRatePercent: 80,
            startedAt,
            out AutonomousPrivateChatSession? session,
            out string sessionError), sessionError);
        return Assert.IsType<AutonomousPrivateChatSession>(session);
    }

    private (AiAccount First, AiAccount Second) CreatePair(string prefix)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            $"{prefix}First",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? first,
            out string firstError), firstError);
        Assert.True(service.TryCreateAiAccount(
            $"{prefix}Second",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? second,
            out string secondError), secondError);
        return (
            Assert.IsType<AiAccount>(first),
            Assert.IsType<AiAccount>(second));
    }

    private RelationshipEvolutionService CreateService()
    {
        return new RelationshipEvolutionService(
            _database.CreateDbContextFactory());
    }

    private static void AssertRelationshipProgress(
        AiRelationship? relationship,
        DateTime occurredAt)
    {
        Assert.NotNull(relationship);
        Assert.Equal(11, relationship.Familiarity);
        Assert.Equal(0, relationship.Affinity);
        Assert.Equal(10, relationship.Trust);
        Assert.Equal(1, relationship.InteractionCount);
        Assert.Equal(occurredAt, relationship.LastInteractionAt);
        Assert.Equal(occurredAt, relationship.UpdatedAt);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

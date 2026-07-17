using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证自主私信判断器的硬性规则、评分、发起者和冷却时间。
/// </summary>
public sealed class AutonomousPrivateChatJudgeTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void GlobalDisabled_StopsBeforeScoring()
    {
        (AiAccount first, AiAccount second) = CreatePair("GlobalOff");

        AutonomousPrivateChatDecision decision = CreateJudge().Evaluate(
            first.Id,
            second.Id,
            DateTime.Now,
            randomJitter: 10);

        Assert.False(decision.IsApproved);
        Assert.Equal(
            AutonomousPrivateChatDecisionStage.GlobalDisabled,
            decision.Stage);
        Assert.Null(decision.InitiatorAiAccountId);
    }

    [Fact]
    public void HighRelationship_SelectsStrongerEligibleInitiatorAndApproves()
    {
        (AiAccount first, AiAccount second) = CreatePair("HighRelation");
        EnableGlobalSettings(AutonomousInteractionFrequency.Normal);
        SetAccountSettings(
            first.Id,
            AutonomousInteractionInitiativeLevel.High,
            canInitiatePrivateChats: true);
        SetAccountSettings(
            second.Id,
            AutonomousInteractionInitiativeLevel.Low,
            canInitiatePrivateChats: true);
        SetRelationship(first.Id, second.Id, 90, 80, 85);
        SetRelationship(second.Id, first.Id, 50, 20, 40);

        AutonomousPrivateChatDecision decision = CreateJudge().Evaluate(
            first.Id,
            second.Id,
            new DateTime(2026, 7, 17, 18, 0, 0),
            randomJitter: -10);

        Assert.True(decision.IsApproved);
        Assert.Equal(first.Id, decision.InitiatorAiAccountId);
        Assert.Equal(second.Id, decision.RecipientAiAccountId);
        Assert.True(decision.RelationshipScore > 80);
        Assert.Equal(10, decision.InitiativeAdjustment);
        Assert.True(decision.FinalScore >= decision.Threshold);
    }

    [Fact]
    public void OnlyEligibleParticipant_BecomesInitiator()
    {
        (AiAccount first, AiAccount second) = CreatePair("SingleInitiator");
        EnableGlobalSettings(AutonomousInteractionFrequency.High);
        SetAccountSettings(
            first.Id,
            AutonomousInteractionInitiativeLevel.High,
            canInitiatePrivateChats: false);
        SetAccountSettings(
            second.Id,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiatePrivateChats: true);
        SetRelationship(second.Id, first.Id, 80, 70, 80);

        AutonomousPrivateChatDecision decision = CreateJudge().Evaluate(
            first.Id,
            second.Id,
            DateTime.Now,
            randomJitter: 0);

        Assert.True(decision.IsApproved);
        Assert.Equal(second.Id, decision.InitiatorAiAccountId);
    }

    [Fact]
    public void DisabledParticipantAndNoEligibleInitiatorAreDistinctStages()
    {
        (AiAccount first, AiAccount second) = CreatePair("Eligibility");
        EnableGlobalSettings(AutonomousInteractionFrequency.Normal);
        SetAccountSettings(
            first.Id,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiatePrivateChats: true,
            isEnabled: false);

        AutonomousPrivateChatDecision disabledDecision = CreateJudge().Evaluate(
            first.Id,
            second.Id,
            DateTime.Now,
            randomJitter: 0);
        Assert.Equal(
            AutonomousPrivateChatDecisionStage.ParticipantDisabled,
            disabledDecision.Stage);

        SetAccountSettings(
            first.Id,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiatePrivateChats: false);
        SetAccountSettings(
            second.Id,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiatePrivateChats: false);

        AutonomousPrivateChatDecision initiatorDecision = CreateJudge().Evaluate(
            first.Id,
            second.Id,
            DateTime.Now,
            randomJitter: 0);
        Assert.Equal(
            AutonomousPrivateChatDecisionStage.NoEligibleInitiator,
            initiatorDecision.Stage);
    }

    [Fact]
    public void RecentInteraction_ActivatesFrequencyCooldown()
    {
        (AiAccount first, AiAccount second) = CreatePair("Cooldown");
        EnableGlobalSettings(AutonomousInteractionFrequency.Normal);
        SetRelationship(first.Id, second.Id, 100, 100, 100);
        DateTime lastInteractionAt = new(2026, 7, 17, 12, 0, 0);
        AiRelationshipOperationStatus recordStatus =
            CreateRelationshipService().TryRecordInteraction(
                first.Id,
                second.Id,
                lastInteractionAt);
        Assert.Equal(AiRelationshipOperationStatus.Success, recordStatus);

        AutonomousPrivateChatDecision decision = CreateJudge().Evaluate(
            first.Id,
            second.Id,
            lastInteractionAt.AddHours(2),
            randomJitter: 10);

        Assert.Equal(
            AutonomousPrivateChatDecisionStage.CooldownActive,
            decision.Stage);
        Assert.Equal(lastInteractionAt.AddHours(8), decision.CooldownEndsAt);
    }

    [Fact]
    public void RandomJitterIsBoundedAndCannotOverrideLowNormalFrequencyScore()
    {
        (AiAccount first, AiAccount second) = CreatePair("BoundedRandom");
        EnableGlobalSettings(AutonomousInteractionFrequency.Normal);

        AutonomousPrivateChatDecision decision = CreateJudge().Evaluate(
            first.Id,
            second.Id,
            DateTime.Now,
            randomJitter: 500);

        Assert.Equal(
            AutonomousPrivateChatDecisionStage.ScoreBelowThreshold,
            decision.Stage);
        Assert.Equal(10, decision.RandomJitter);
        Assert.True(decision.FinalScore < decision.Threshold);
    }

    private (AiAccount First, AiAccount Second) CreatePair(string prefix)
    {
        return (
            CreateAccount($"{prefix}A"),
            CreateAccount($"{prefix}B"));
    }

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        bool succeeded = service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage);
        Assert.True(succeeded, errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private void EnableGlobalSettings(AutonomousInteractionFrequency frequency)
    {
        AutonomousInteractionSettingsService service = new(
            _database.CreateDbContextFactory());
        Assert.True(service.TryUpdateSettings(
            isEnabled: true,
            frequency,
            allowPrivateChats: true,
            allowGroupChats: true,
            out _,
            out string errorMessage), errorMessage);
    }

    private void SetAccountSettings(
        Guid aiAccountId,
        AutonomousInteractionInitiativeLevel initiativeLevel,
        bool canInitiatePrivateChats,
        bool isEnabled = true)
    {
        AiAccountAutonomySettingsService service = new(
            _database.CreateDbContextFactory());
        Assert.True(service.TryUpdateSettings(
            aiAccountId,
            isEnabled,
            initiativeLevel,
            canInitiatePrivateChats,
            canInitiateGroupChats: true,
            canJoinGroupChats: true,
            out _));
    }

    private void SetRelationship(
        Guid fromAiAccountId,
        Guid toAiAccountId,
        int familiarity,
        int affinity,
        int trust)
    {
        AiRelationshipOperationStatus status =
            CreateRelationshipService().TryUpdateRelationship(
                fromAiAccountId,
                toAiAccountId,
                familiarity,
                affinity,
                trust,
                out _);
        Assert.Equal(AiRelationshipOperationStatus.Success, status);
    }

    private AutonomousPrivateChatJudge CreateJudge()
    {
        return new AutonomousPrivateChatJudge(
            _database.CreateDbContextFactory());
    }

    private AiRelationshipService CreateRelationshipService()
    {
        return new AiRelationshipService(
            _database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

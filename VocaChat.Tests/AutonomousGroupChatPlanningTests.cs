using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证自主好友群聊的成员边界、权限、关系评分和话题计划。
/// </summary>
public sealed class AutonomousGroupChatPlanningTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void Evaluate_WithFewerThanThreeParticipants_IsRejected()
    {
        AiAccount first = CreateAccount("FewA");
        AiAccount second = CreateAccount("FewB");

        AutonomousGroupChatDecision decision = CreateJudge().Evaluate(
            new[] { first.Id, second.Id },
            randomJitter: 0);

        Assert.Equal(
            AutonomousGroupChatDecisionStage.TooFewParticipants,
            decision.Stage);
    }

    [Fact]
    public void Evaluate_WhenGlobalInteractionIsDisabled_StopsBeforeScoring()
    {
        IReadOnlyList<AiAccount> accounts = CreateGroup("GlobalOff");

        AutonomousGroupChatDecision decision = CreateJudge().Evaluate(
            accounts.Select(account => account.Id),
            randomJitter: 10);

        Assert.Equal(
            AutonomousGroupChatDecisionStage.GlobalDisabled,
            decision.Stage);
        Assert.Null(decision.InitiatorAiAccountId);
    }

    [Fact]
    public void Evaluate_WithWeakDefaultRelationships_IsRejectedAtNormalFrequency()
    {
        IReadOnlyList<AiAccount> accounts = CreateGroup("Weak");
        EnableGroupChats();

        AutonomousGroupChatDecision decision = CreateJudge().Evaluate(
            accounts.Select(account => account.Id),
            randomJitter: 0);

        Assert.Equal(
            AutonomousGroupChatDecisionStage.ScoreBelowThreshold,
            decision.Stage);
        Assert.True(decision.FinalScore < decision.Threshold);
    }

    [Fact]
    public void Evaluate_UsesConfiguredMaximumWithoutFixedProductCeiling()
    {
        IReadOnlyList<AiAccount> accounts = Enumerable.Range(1, 5)
            .Select(index => CreateAccount($"Maximum{index}"))
            .ToList()
            .AsReadOnly();
        EnableGroupChats(maximumMembers: 4);

        AutonomousGroupChatDecision decision = CreateJudge().Evaluate(
            accounts.Select(account => account.Id),
            randomJitter: 0);

        Assert.Equal(
            AutonomousGroupChatDecisionStage.TooManyParticipants,
            decision.Stage);
        Assert.Equal(4, decision.MaximumMembers);
    }

    [Fact]
    public void Evaluate_RequiresJoinPermissionAndEligibleInitiator()
    {
        IReadOnlyList<AiAccount> accounts = CreateGroup("Permissions");
        EnableGroupChats();
        SetAutonomy(
            accounts[2].Id,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiate: true,
            canJoin: false);

        AutonomousGroupChatDecision cannotJoin = CreateJudge().Evaluate(
            accounts.Select(account => account.Id),
            randomJitter: 0);

        Assert.Equal(
            AutonomousGroupChatDecisionStage.ParticipantCannotJoin,
            cannotJoin.Stage);

        foreach (AiAccount account in accounts)
        {
            SetAutonomy(
                account.Id,
                AutonomousInteractionInitiativeLevel.Normal,
                canInitiate: false,
                canJoin: true);
        }

        AutonomousGroupChatDecision noInitiator = CreateJudge().Evaluate(
            accounts.Select(account => account.Id),
            randomJitter: 0);

        Assert.Equal(
            AutonomousGroupChatDecisionStage.NoEligibleInitiator,
            noInitiator.Stage);
    }

    [Fact]
    public void StrongGroup_SelectsEligibleInitiatorAndCreatesRequestedPlan()
    {
        IReadOnlyList<AiAccount> accounts = CreateGroup("Strong");
        EnableGroupChats();
        SetAutonomy(
            accounts[0].Id,
            AutonomousInteractionInitiativeLevel.High,
            canInitiate: true,
            canJoin: true);
        SetAutonomy(
            accounts[1].Id,
            AutonomousInteractionInitiativeLevel.Low,
            canInitiate: true,
            canJoin: true);
        SetAutonomy(
            accounts[2].Id,
            AutonomousInteractionInitiativeLevel.Normal,
            canInitiate: false,
            canJoin: true);
        SetStrongRelationships(accounts);

        AutonomousGroupChatDecision decision = CreateJudge().Evaluate(
            accounts.Select(account => account.Id),
            randomJitter: -10);
        bool planned = CreatePlanningService().TryCreatePlan(
            decision,
            "周末一起去哪里",
            out AutonomousGroupChatPlan? plan,
            out string errorMessage);

        Assert.True(decision.IsApproved);
        Assert.Equal(accounts[0].Id, decision.InitiatorAiAccountId);
        Assert.True(planned, errorMessage);
        Assert.NotNull(plan);
        Assert.Equal("周末一起去哪里", plan.Topic);
        Assert.False(plan.IncludesLocalUser);
        Assert.Equal(
            accounts.Select(account => account.Id),
            plan.MemberAiAccountIds);
    }

    [Fact]
    public void Planning_WithoutRequestedTopic_UsesSharedInterest()
    {
        IReadOnlyList<AiAccount> accounts = new[]
        {
            CreateAccount("InterestA", "摄影"),
            CreateAccount("InterestB", "摄影"),
            CreateAccount("InterestC", "阅读")
        };
        EnableGroupChats(AutonomousInteractionFrequency.High);
        SetStrongRelationships(accounts);
        AutonomousGroupChatDecision decision = CreateJudge().Evaluate(
            accounts.Select(account => account.Id),
            randomJitter: 0);

        bool planned = CreatePlanningService().TryCreatePlan(
            decision,
            requestedTopic: null,
            out AutonomousGroupChatPlan? plan,
            out string errorMessage);

        Assert.True(planned, errorMessage);
        Assert.Equal("摄影", plan!.Topic);
        Assert.True(decision.SharedInterestBonus > 0);
    }

    private IReadOnlyList<AiAccount> CreateGroup(string prefix)
    {
        return Enumerable.Range(1, 3)
            .Select(index => CreateAccount($"{prefix}{index}"))
            .ToList()
            .AsReadOnly();
    }

    private AiAccount CreateAccount(string nickname, string? interest = null)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        bool succeeded = service.TryCreateAiAccount(
            new AiAccountCreationData
            {
                Nickname = nickname,
                InterestTags = interest is null
                    ? Array.Empty<string>()
                    : new[] { interest }
            },
            out AiAccount? account,
            out string errorMessage);
        Assert.True(succeeded, errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private void EnableGroupChats(
        AutonomousInteractionFrequency frequency =
            AutonomousInteractionFrequency.Normal,
        int maximumMembers = 6)
    {
        AutonomousInteractionSettingsService service = new(
            _database.CreateDbContextFactory());
        Assert.True(service.TryUpdateSettings(
            isEnabled: true,
            frequency,
            allowPrivateChats: true,
            allowGroupChats: true,
            privateChatContinuationRatePercent: 80,
            privateChatMaximumRounds: 6,
            autonomousGroupChatMaximumMembers: maximumMembers,
            out _,
            out string errorMessage), errorMessage);
    }

    private void SetAutonomy(
        Guid accountId,
        AutonomousInteractionInitiativeLevel initiativeLevel,
        bool canInitiate,
        bool canJoin)
    {
        AiAccountAutonomySettingsService service = new(
            _database.CreateDbContextFactory());
        Assert.True(service.TryUpdateSettings(
            accountId,
            isEnabled: true,
            initiativeLevel,
            canInitiatePrivateChats: true,
            canInitiateGroupChats: canInitiate,
            canJoinGroupChats: canJoin,
            out _));
    }

    private void SetStrongRelationships(IReadOnlyList<AiAccount> accounts)
    {
        AiRelationshipService service = new(
            _database.CreateDbContextFactory());

        foreach (AiAccount from in accounts)
        {
            foreach (AiAccount to in accounts.Where(to => to.Id != from.Id))
            {
                Assert.Equal(
                    AiRelationshipOperationStatus.Success,
                    service.TryUpdateRelationship(
                        from.Id,
                        to.Id,
                        familiarity: 90,
                        affinity: 80,
                        trust: 85,
                        out _));
            }
        }
    }

    private AutonomousGroupChatJudge CreateJudge()
    {
        return new AutonomousGroupChatJudge(
            _database.CreateDbContextFactory());
    }

    private AutonomousGroupChatPlanningService CreatePlanningService()
    {
        return new AutonomousGroupChatPlanningService(
            _database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

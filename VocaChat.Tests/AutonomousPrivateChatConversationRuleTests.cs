using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证普通轮形式、递减概率和最终收束的核心纯规则。
/// </summary>
public sealed class AutonomousPrivateChatConversationRuleTests
{
    [Fact]
    public void ContinuationProbability_StrictlyDecreasesFromPreviousRound()
    {
        AutonomousPrivateChatContinuationDecider decider = new();
        AutonomousPrivateChatPlan plan = CreatePlan(
            mutualRelationshipScore: 85,
            continuationRatePercent: 80);
        AutonomousPrivateChatRoundPlan previousRound = new()
        {
            InitiatorMessageMode = AutonomousPrivateChatMessageMode.Single,
            InitiatorMessageCount = 1,
            RecipientMessageMode = AutonomousPrivateChatMessageMode.Burst,
            RecipientMessageCount = 2
        };

        AutonomousPrivateChatContinuationDecision first = decider.Decide(
            plan,
            previousOccurrenceProbability: 1,
            previousRound,
            previousRoundNaturallyClosed: false,
            randomRoll: 0.1);
        AutonomousPrivateChatContinuationDecision second = decider.Decide(
            plan,
            first.OccurrenceProbability,
            previousRound,
            previousRoundNaturallyClosed: false,
            randomRoll: 0.1);

        Assert.True(first.OccurrenceProbability < 1);
        Assert.True(second.OccurrenceProbability < first.OccurrenceProbability);
        Assert.True(first.ShouldContinue);
        Assert.True(second.ShouldContinue);
    }

    [Fact]
    public void ContinuationProbability_IsReducedWhenRecipientDoesNotReply()
    {
        AutonomousPrivateChatContinuationDecider decider = new();
        AutonomousPrivateChatPlan plan = CreatePlan(50, 80);
        AutonomousPrivateChatRoundPlan replied = CreatePreviousRound(
            AutonomousPrivateChatMessageMode.Single,
            recipientMessageCount: 1);
        AutonomousPrivateChatRoundPlan ignored = CreatePreviousRound(
            AutonomousPrivateChatMessageMode.None,
            recipientMessageCount: 0);

        double repliedProbability = decider.Decide(
            plan, 1, replied, false, 0.99).OccurrenceProbability;
        double ignoredProbability = decider.Decide(
            plan, 1, ignored, false, 0.99).OccurrenceProbability;

        Assert.True(ignoredProbability < repliedProbability);
    }

    [Fact]
    public void ContinuationProbability_IsReducedAfterRepeatedLowInformation()
    {
        AutonomousPrivateChatContinuationDecider decider = new();
        AutonomousPrivateChatPlan plan = CreatePlan(70, 80);
        AutonomousPrivateChatRoundPlan previousRound =
            CreatePreviousRound(
                AutonomousPrivateChatMessageMode.Single,
                recipientMessageCount: 1);

        double normalProbability = decider.Decide(
            plan,
            1,
            previousRound,
            previousRoundNaturallyClosed: false,
            randomRoll: 0.99).OccurrenceProbability;
        double lowInformationProbability = decider.Decide(
            plan,
            1,
            previousRound,
            previousRoundNaturallyClosed: false,
            randomRoll: 0.99,
            consecutiveLowInformationRounds: 2).OccurrenceProbability;

        Assert.True(lowInformationProbability < normalProbability);
    }

    [Fact]
    public void ContinuationProbability_IsZeroAfterNaturalClosingMessage()
    {
        AutonomousPrivateChatContinuationDecision decision =
            new AutonomousPrivateChatContinuationDecider().Decide(
                CreatePlan(80, 95),
                1,
                CreatePreviousRound(
                    AutonomousPrivateChatMessageMode.Burst,
                    recipientMessageCount: 2),
                previousRoundNaturallyClosed: true,
                randomRoll: 0);

        Assert.Equal(0, decision.OccurrenceProbability);
        Assert.False(decision.ShouldContinue);
    }

    [Theory]
    [InlineData(0.00, AutonomousPrivateChatMessageMode.None, 0)]
    [InlineData(0.50, AutonomousPrivateChatMessageMode.Single, 1)]
    [InlineData(0.90, AutonomousPrivateChatMessageMode.Burst, 2)]
    public void RoundPlanner_ProducesRelationshipAwareRecipientModes(
        double recipientRoll,
        AutonomousPrivateChatMessageMode expectedMode,
        int expectedCount)
    {
        AutonomousPrivateChatRoundPlan result =
            new AutonomousPrivateChatRoundPlanner().Plan(
                CreatePlan(mutualRelationshipScore: 20, continuationRatePercent: 80),
                initiatorModeRoll: 0.9,
                initiatorBurstSizeRoll: 0,
                recipientModeRoll: recipientRoll,
                recipientBurstSizeRoll: 0);

        Assert.Equal(expectedMode, result.RecipientMessageMode);
        Assert.Equal(expectedCount, result.RecipientMessageCount);
        Assert.NotEqual(AutonomousPrivateChatMessageMode.None, result.InitiatorMessageMode);
        Assert.True(result.InitiatorMessageCount >= 1);
    }

    [Theory]
    [InlineData(0.00, AutonomousPrivateChatMessageMode.None, AutonomousPrivateChatMessageMode.None)]
    [InlineData(0.50, AutonomousPrivateChatMessageMode.Single, AutonomousPrivateChatMessageMode.None)]
    [InlineData(0.70, AutonomousPrivateChatMessageMode.None, AutonomousPrivateChatMessageMode.Single)]
    [InlineData(0.99, AutonomousPrivateChatMessageMode.Single, AutonomousPrivateChatMessageMode.Single)]
    public void ClosurePlanner_CoversFourClosingForms(
        double modeRoll,
        AutonomousPrivateChatMessageMode expectedInitiatorMode,
        AutonomousPrivateChatMessageMode expectedRecipientMode)
    {
        AutonomousPrivateChatRoundPlan result =
            new AutonomousPrivateChatClosurePlanner().Plan(
                CreatePlan(40, 80),
                CreatePreviousRound(
                    AutonomousPrivateChatMessageMode.Single,
                    recipientMessageCount: 1),
                "这次聊得很有意思。",
                modeRoll,
                initiatorBurstRoll: 0,
                recipientBurstRoll: 0);

        Assert.Equal(expectedInitiatorMode, result.InitiatorMessageMode);
        Assert.Equal(expectedRecipientMode, result.RecipientMessageMode);
    }

    private static AutonomousPrivateChatPlan CreatePlan(
        double mutualRelationshipScore,
        int continuationRatePercent)
    {
        return new AutonomousPrivateChatPlan
        {
            Topic = "近况",
            MaximumRounds = 6,
            ContinuationRatePercent = continuationRatePercent,
            InitiatorToRecipientRelationshipScore = mutualRelationshipScore,
            RecipientToInitiatorRelationshipScore = mutualRelationshipScore,
            MutualRelationshipScore = mutualRelationshipScore,
            InitiatorInitiativeLevel = AutonomousInteractionInitiativeLevel.Normal
        };
    }

    private static AutonomousPrivateChatRoundPlan CreatePreviousRound(
        AutonomousPrivateChatMessageMode recipientMode,
        int recipientMessageCount)
    {
        return new AutonomousPrivateChatRoundPlan
        {
            InitiatorMessageMode = AutonomousPrivateChatMessageMode.Single,
            InitiatorMessageCount = 1,
            RecipientMessageMode = recipientMode,
            RecipientMessageCount = recipientMessageCount
        };
    }
}

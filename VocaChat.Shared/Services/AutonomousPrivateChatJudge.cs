using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 根据全局设置、好友设置和双向关系评估两个好友是否应发起私信。
/// </summary>
public sealed class AutonomousPrivateChatJudge
{
    private const double MinimumRandomJitter = -10;
    private const double MaximumRandomJitter = 10;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousPrivateChatJudge(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 执行一次不产生任何数据库写入的自主私信判断。
    /// </summary>
    public AutonomousPrivateChatDecision Evaluate(
        Guid firstAiAccountId,
        Guid secondAiAccountId,
        DateTime evaluatedAt,
        double randomJitter)
    {
        if (firstAiAccountId == secondAiAccountId)
        {
            return CreateStoppedDecision(
                AutonomousPrivateChatDecisionStage.SelfInteractionNotAllowed,
                firstAiAccountId,
                secondAiAccountId);
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        int existingAccountCount = dbContext.AiAccounts.Count(account =>
            account.Id == firstAiAccountId
            || account.Id == secondAiAccountId);

        if (existingAccountCount != 2)
        {
            return CreateStoppedDecision(
                AutonomousPrivateChatDecisionStage.AccountNotFound,
                firstAiAccountId,
                secondAiAccountId);
        }

        AutonomousInteractionSettings globalSettings =
            dbContext.AutonomousInteractionSettings
                .AsNoTracking()
                .SingleOrDefault(settings =>
                    settings.Id == AutonomousInteractionSettings.SingletonId)
            ?? new AutonomousInteractionSettings();

        if (!globalSettings.IsEnabled)
        {
            return CreateStoppedDecision(
                AutonomousPrivateChatDecisionStage.GlobalDisabled,
                firstAiAccountId,
                secondAiAccountId);
        }

        if (!globalSettings.AllowPrivateChats)
        {
            return CreateStoppedDecision(
                AutonomousPrivateChatDecisionStage.PrivateChatsDisabled,
                firstAiAccountId,
                secondAiAccountId);
        }

        Dictionary<Guid, AiAccountAutonomySettings> accountSettings =
            dbContext.AiAccountAutonomySettings
                .AsNoTracking()
                .Where(settings =>
                    settings.AiAccountId == firstAiAccountId
                    || settings.AiAccountId == secondAiAccountId)
                .ToDictionary(settings => settings.AiAccountId);
        AiAccountAutonomySettings firstSettings =
            accountSettings.GetValueOrDefault(firstAiAccountId)
            ?? new AiAccountAutonomySettings(firstAiAccountId);
        AiAccountAutonomySettings secondSettings =
            accountSettings.GetValueOrDefault(secondAiAccountId)
            ?? new AiAccountAutonomySettings(secondAiAccountId);

        if (!firstSettings.IsEnabled || !secondSettings.IsEnabled)
        {
            return CreateStoppedDecision(
                AutonomousPrivateChatDecisionStage.ParticipantDisabled,
                firstAiAccountId,
                secondAiAccountId);
        }

        if (!firstSettings.CanInitiatePrivateChats
            && !secondSettings.CanInitiatePrivateChats)
        {
            return CreateStoppedDecision(
                AutonomousPrivateChatDecisionStage.NoEligibleInitiator,
                firstAiAccountId,
                secondAiAccountId);
        }

        Dictionary<Guid, AiRelationship> relationships =
            dbContext.AiRelationships
                .AsNoTracking()
                .Where(relationship =>
                    (relationship.FromAiAccountId == firstAiAccountId
                        && relationship.ToAiAccountId == secondAiAccountId)
                    || (relationship.FromAiAccountId == secondAiAccountId
                        && relationship.ToAiAccountId == firstAiAccountId))
                .ToDictionary(relationship => relationship.FromAiAccountId);
        AiRelationship firstToSecond =
            relationships.GetValueOrDefault(firstAiAccountId)
            ?? new AiRelationship(firstAiAccountId, secondAiAccountId);
        AiRelationship secondToFirst =
            relationships.GetValueOrDefault(secondAiAccountId)
            ?? new AiRelationship(secondAiAccountId, firstAiAccountId);

        CandidateScore? firstCandidate = firstSettings.CanInitiatePrivateChats
            ? CalculateCandidateScore(firstSettings, firstToSecond)
            : null;
        CandidateScore? secondCandidate = secondSettings.CanInitiatePrivateChats
            ? CalculateCandidateScore(secondSettings, secondToFirst)
            : null;
        bool firstInitiates = firstCandidate is not null
            && (secondCandidate is null
                || firstCandidate.FinalScore > secondCandidate.FinalScore
                || (firstCandidate.FinalScore == secondCandidate.FinalScore
                    && firstAiAccountId.CompareTo(secondAiAccountId) <= 0));
        CandidateScore selectedCandidate = firstInitiates
            ? firstCandidate!
            : secondCandidate!;
        Guid initiatorAiAccountId = firstInitiates
            ? firstAiAccountId
            : secondAiAccountId;
        Guid recipientAiAccountId = firstInitiates
            ? secondAiAccountId
            : firstAiAccountId;

        double threshold = GetThreshold(globalSettings.Frequency);
        TimeSpan cooldown = GetCooldown(globalSettings.Frequency);
        DateTime? lastInteractionAt = GetLatestInteractionAt(
            firstToSecond,
            secondToFirst);
        DateTime? cooldownEndsAt = lastInteractionAt?.Add(cooldown);
        double boundedRandomJitter = Math.Clamp(
            randomJitter,
            MinimumRandomJitter,
            MaximumRandomJitter);
        double finalScore = Math.Clamp(
            selectedCandidate.FinalScore + boundedRandomJitter,
            0,
            100);

        AutonomousPrivateChatDecisionStage stage =
            cooldownEndsAt > evaluatedAt
                ? AutonomousPrivateChatDecisionStage.CooldownActive
                : finalScore >= threshold
                    ? AutonomousPrivateChatDecisionStage.Approved
                    : AutonomousPrivateChatDecisionStage.ScoreBelowThreshold;

        return new AutonomousPrivateChatDecision
        {
            Stage = stage,
            FirstAiAccountId = firstAiAccountId,
            SecondAiAccountId = secondAiAccountId,
            InitiatorAiAccountId = initiatorAiAccountId,
            RecipientAiAccountId = recipientAiAccountId,
            RelationshipScore = Round(selectedCandidate.RelationshipScore),
            InitiativeAdjustment = selectedCandidate.InitiativeAdjustment,
            RandomJitter = Round(boundedRandomJitter),
            FinalScore = Round(finalScore),
            Threshold = threshold,
            CooldownEndsAt = cooldownEndsAt
        };
    }

    private static CandidateScore CalculateCandidateScore(
        AiAccountAutonomySettings settings,
        AiRelationship relationship)
    {
        double relationshipScore =
            AiRelationshipScoring.Calculate(relationship);
        int initiativeAdjustment = settings.InitiativeLevel switch
        {
            AutonomousInteractionInitiativeLevel.Low => -10,
            AutonomousInteractionInitiativeLevel.High => 10,
            _ => 0
        };

        return new CandidateScore(
            relationshipScore,
            initiativeAdjustment,
            Math.Clamp(relationshipScore + initiativeAdjustment, 0, 100));
    }

    private static double GetThreshold(AutonomousInteractionFrequency frequency)
    {
        return frequency switch
        {
            AutonomousInteractionFrequency.Low => 70,
            AutonomousInteractionFrequency.High => 35,
            _ => 50
        };
    }

    private static TimeSpan GetCooldown(
        AutonomousInteractionFrequency frequency)
    {
        return frequency switch
        {
            AutonomousInteractionFrequency.Low => TimeSpan.FromHours(24),
            AutonomousInteractionFrequency.High => TimeSpan.FromHours(2),
            _ => TimeSpan.FromHours(8)
        };
    }

    private static DateTime? GetLatestInteractionAt(
        AiRelationship firstToSecond,
        AiRelationship secondToFirst)
    {
        if (firstToSecond.LastInteractionAt is null)
        {
            return secondToFirst.LastInteractionAt;
        }

        if (secondToFirst.LastInteractionAt is null)
        {
            return firstToSecond.LastInteractionAt;
        }

        return firstToSecond.LastInteractionAt >= secondToFirst.LastInteractionAt
            ? firstToSecond.LastInteractionAt
            : secondToFirst.LastInteractionAt;
    }

    private static AutonomousPrivateChatDecision CreateStoppedDecision(
        AutonomousPrivateChatDecisionStage stage,
        Guid firstAiAccountId,
        Guid secondAiAccountId)
    {
        return new AutonomousPrivateChatDecision
        {
            Stage = stage,
            FirstAiAccountId = firstAiAccountId,
            SecondAiAccountId = secondAiAccountId
        };
    }

    private static double Round(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private sealed record CandidateScore(
        double RelationshipScore,
        int InitiativeAdjustment,
        double FinalScore);
}

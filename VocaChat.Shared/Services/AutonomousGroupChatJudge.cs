using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 根据全局设置、好友权限、双向关系和共同兴趣判断一组好友是否适合自主建群。
/// </summary>
public sealed class AutonomousGroupChatJudge
{
    private const int MinimumParticipantCount = 3;
    private const double MinimumRandomJitter = -10;
    private const double MaximumRandomJitter = 10;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousGroupChatJudge(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 对一个已经提出的成员组合执行只读判断，不创建群聊或消息。
    /// </summary>
    public AutonomousGroupChatDecision Evaluate(
        IEnumerable<Guid> participantAiAccountIds,
        double randomJitter)
    {
        IReadOnlyList<Guid> submittedIds = participantAiAccountIds is null
            ? Array.Empty<Guid>()
            : participantAiAccountIds.ToList().AsReadOnly();
        IReadOnlyList<Guid> distinctIds = submittedIds
            .Distinct()
            .ToList()
            .AsReadOnly();

        if (submittedIds.Count != distinctIds.Count)
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.DuplicateParticipant,
                distinctIds);
        }

        if (distinctIds.Count < MinimumParticipantCount)
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.TooFewParticipants,
                distinctIds);
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousInteractionSettings globalSettings =
            dbContext.AutonomousInteractionSettings
                .AsNoTracking()
                .SingleOrDefault(settings =>
                    settings.Id == AutonomousInteractionSettings.SingletonId)
            ?? new AutonomousInteractionSettings();

        if (distinctIds.Count
            > globalSettings.AutonomousGroupChatMaximumMembers)
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.TooManyParticipants,
                distinctIds,
                globalSettings.AutonomousGroupChatMaximumMembers);
        }

        List<AiAccount> participants = dbContext.AiAccounts
            .AsNoTracking()
            .Include(account => account.Tags)
            .Where(account => distinctIds.Contains(account.Id))
            .ToList();

        if (participants.Count != distinctIds.Count)
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.AccountNotFound,
                distinctIds,
                globalSettings.AutonomousGroupChatMaximumMembers);
        }

        if (!globalSettings.IsEnabled)
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.GlobalDisabled,
                distinctIds,
                globalSettings.AutonomousGroupChatMaximumMembers);
        }

        if (!globalSettings.AllowGroupChats)
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.GroupChatsDisabled,
                distinctIds,
                globalSettings.AutonomousGroupChatMaximumMembers);
        }

        Dictionary<Guid, AiAccountAutonomySettings> storedAccountSettings =
            dbContext.AiAccountAutonomySettings
                .AsNoTracking()
                .Where(settings => distinctIds.Contains(settings.AiAccountId))
                .ToDictionary(settings => settings.AiAccountId);
        Dictionary<Guid, AiAccountAutonomySettings> accountSettings =
            distinctIds.ToDictionary(
                id => id,
                id => storedAccountSettings.GetValueOrDefault(id)
                    ?? new AiAccountAutonomySettings(id));

        if (accountSettings.Values.Any(settings => !settings.IsEnabled))
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.ParticipantDisabled,
                distinctIds,
                globalSettings.AutonomousGroupChatMaximumMembers);
        }

        if (accountSettings.Values.Any(settings => !settings.CanJoinGroupChats))
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.ParticipantCannotJoin,
                distinctIds,
                globalSettings.AutonomousGroupChatMaximumMembers);
        }

        IReadOnlyList<Guid> eligibleInitiatorIds = accountSettings.Values
            .Where(settings => settings.CanInitiateGroupChats)
            .Select(settings => settings.AiAccountId)
            .ToList()
            .AsReadOnly();

        if (eligibleInitiatorIds.Count == 0)
        {
            return CreateStoppedDecision(
                AutonomousGroupChatDecisionStage.NoEligibleInitiator,
                distinctIds,
                globalSettings.AutonomousGroupChatMaximumMembers);
        }

        Dictionary<(Guid FromId, Guid ToId), AiRelationship> relationships =
            dbContext.AiRelationships
                .AsNoTracking()
                .Where(relationship =>
                    distinctIds.Contains(relationship.FromAiAccountId)
                    && distinctIds.Contains(relationship.ToAiAccountId))
                .ToDictionary(relationship => (
                    relationship.FromAiAccountId,
                    relationship.ToAiAccountId));
        Dictionary<(Guid FromId, Guid ToId), double> directionalScores =
            CreateDirectionalScores(distinctIds, relationships);
        List<double> mutualScores = CreateMutualScores(
            distinctIds,
            directionalScores);
        double averageRelationshipScore = mutualScores.Average();
        double weakestRelationshipScore = mutualScores.Min();
        double sharedInterestBonus = CalculateSharedInterestBonus(participants);
        (Guid InitiatorId, int InitiativeAdjustment) = SelectInitiator(
            eligibleInitiatorIds,
            distinctIds,
            accountSettings,
            directionalScores);
        double boundedRandomJitter = Math.Clamp(
            randomJitter,
            MinimumRandomJitter,
            MaximumRandomJitter);
        double threshold = GetThreshold(globalSettings.Frequency);
        double finalScore = Math.Clamp(
            averageRelationshipScore * 0.7
                + weakestRelationshipScore * 0.3
                + sharedInterestBonus
                + InitiativeAdjustment
                + boundedRandomJitter,
            0,
            100);

        return new AutonomousGroupChatDecision
        {
            Stage = finalScore >= threshold
                ? AutonomousGroupChatDecisionStage.Approved
                : AutonomousGroupChatDecisionStage.ScoreBelowThreshold,
            ParticipantAiAccountIds = distinctIds,
            InitiatorAiAccountId = InitiatorId,
            MaximumMembers = globalSettings.AutonomousGroupChatMaximumMembers,
            AverageRelationshipScore = Round(averageRelationshipScore),
            WeakestRelationshipScore = Round(weakestRelationshipScore),
            SharedInterestBonus = Round(sharedInterestBonus),
            InitiativeAdjustment = InitiativeAdjustment,
            RandomJitter = Round(boundedRandomJitter),
            FinalScore = Round(finalScore),
            Threshold = threshold
        };
    }

    private static Dictionary<(Guid FromId, Guid ToId), double>
        CreateDirectionalScores(
            IReadOnlyList<Guid> participantIds,
            IReadOnlyDictionary<(Guid FromId, Guid ToId), AiRelationship>
                relationships)
    {
        Dictionary<(Guid FromId, Guid ToId), double> scores = new();

        foreach (Guid fromId in participantIds)
        {
            foreach (Guid toId in participantIds.Where(id => id != fromId))
            {
                AiRelationship relationship = relationships.GetValueOrDefault(
                    (fromId, toId))
                    ?? new AiRelationship(fromId, toId);
                scores[(fromId, toId)] =
                    AiRelationshipScoring.Calculate(relationship);
            }
        }

        return scores;
    }

    private static List<double> CreateMutualScores(
        IReadOnlyList<Guid> participantIds,
        IReadOnlyDictionary<(Guid FromId, Guid ToId), double>
            directionalScores)
    {
        List<double> scores = new();

        for (int firstIndex = 0;
             firstIndex < participantIds.Count - 1;
             firstIndex++)
        {
            for (int secondIndex = firstIndex + 1;
                 secondIndex < participantIds.Count;
                 secondIndex++)
            {
                Guid firstId = participantIds[firstIndex];
                Guid secondId = participantIds[secondIndex];
                scores.Add(AiRelationshipScoring.CalculateMutual(
                    directionalScores[(firstId, secondId)],
                    directionalScores[(secondId, firstId)]));
            }
        }

        return scores;
    }

    private static double CalculateSharedInterestBonus(
        IReadOnlyList<AiAccount> participants)
    {
        int pairCount = 0;
        int pairsWithSharedInterest = 0;

        for (int firstIndex = 0;
             firstIndex < participants.Count - 1;
             firstIndex++)
        {
            HashSet<string> firstInterests = participants[firstIndex].Tags
                .Where(tag => tag.Type == AiAccountTagType.Interest)
                .Select(tag => tag.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (int secondIndex = firstIndex + 1;
                 secondIndex < participants.Count;
                 secondIndex++)
            {
                pairCount++;
                bool sharesInterest = participants[secondIndex].Tags
                    .Where(tag => tag.Type == AiAccountTagType.Interest)
                    .Select(tag => tag.Value)
                    .Any(firstInterests.Contains);

                if (sharesInterest)
                {
                    pairsWithSharedInterest++;
                }
            }
        }

        return pairCount == 0
            ? 0
            : 10d * pairsWithSharedInterest / pairCount;
    }

    private static (Guid InitiatorId, int InitiativeAdjustment)
        SelectInitiator(
            IReadOnlyList<Guid> eligibleInitiatorIds,
            IReadOnlyList<Guid> participantIds,
            IReadOnlyDictionary<Guid, AiAccountAutonomySettings>
                accountSettings,
            IReadOnlyDictionary<(Guid FromId, Guid ToId), double>
                directionalScores)
    {
        return eligibleInitiatorIds
            .Select(id =>
            {
                double averageOutgoingRelationship = participantIds
                    .Where(otherId => otherId != id)
                    .Average(otherId => directionalScores[(id, otherId)]);
                int initiativeAdjustment = GetInitiativeAdjustment(
                    accountSettings[id].InitiativeLevel);
                return new
                {
                    Id = id,
                    InitiativeAdjustment = initiativeAdjustment,
                    Score = averageOutgoingRelationship + initiativeAdjustment
                };
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => (
                candidate.Id,
                candidate.InitiativeAdjustment))
            .First();
    }

    private static int GetInitiativeAdjustment(
        AutonomousInteractionInitiativeLevel level)
    {
        return level switch
        {
            AutonomousInteractionInitiativeLevel.Low => -10,
            AutonomousInteractionInitiativeLevel.High => 10,
            _ => 0
        };
    }

    private static double GetThreshold(
        AutonomousInteractionFrequency frequency)
    {
        return frequency switch
        {
            AutonomousInteractionFrequency.Low => 70,
            AutonomousInteractionFrequency.High => 35,
            _ => 50
        };
    }

    private static AutonomousGroupChatDecision CreateStoppedDecision(
        AutonomousGroupChatDecisionStage stage,
        IReadOnlyList<Guid> participantIds,
        int maximumMembers =
            AutonomousInteractionSettings.DefaultAutonomousGroupChatMaximumMembers)
    {
        return new AutonomousGroupChatDecision
        {
            Stage = stage,
            ParticipantAiAccountIds = participantIds,
            MaximumMembers = maximumMembers
        };
    }

    private static double Round(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 按成员关系、上一轮参与情况和当前回复目标选择本轮少量发言者。
/// </summary>
public sealed class AutonomousGroupChatSpeakerPlanner
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousGroupChatSpeakerPlanner(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public IReadOnlyList<Guid> Plan(
        AutonomousGroupChatPlan plan,
        IReadOnlyCollection<Guid> previousRoundSpeakerIds,
        Guid? latestSpeakerId,
        int desiredSpeakerCount,
        bool requireInitiator)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(previousRoundSpeakerIds);

        int boundedCount = Math.Clamp(
            desiredSpeakerCount,
            0,
            plan.MemberAiAccountIds.Count);
        if (boundedCount == 0)
        {
            return Array.Empty<Guid>();
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        HashSet<Guid> memberIds = plan.MemberAiAccountIds.ToHashSet();
        Dictionary<(Guid FromId, Guid ToId), AiRelationship> relationships =
            dbContext.AiRelationships
                .AsNoTracking()
                .Where(relationship =>
                    memberIds.Contains(relationship.FromAiAccountId)
                    && memberIds.Contains(relationship.ToAiAccountId))
                .ToDictionary(relationship => (
                    relationship.FromAiAccountId,
                    relationship.ToAiAccountId));

        List<Guid> speakers = new();
        if (requireInitiator)
        {
            speakers.Add(plan.InitiatorAiAccountId);
        }

        speakers.AddRange(plan.MemberAiAccountIds
            .Where(id => !speakers.Contains(id))
            .Select((id, index) => new
            {
                Id = id,
                OriginalIndex = index,
                Score = CalculateSpeakerScore(
                    id,
                    plan,
                    previousRoundSpeakerIds,
                    latestSpeakerId,
                    relationships)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.OriginalIndex)
            .Take(boundedCount - speakers.Count)
            .Select(item => item.Id));

        return speakers.AsReadOnly();
    }

    private static double CalculateSpeakerScore(
        Guid candidateId,
        AutonomousGroupChatPlan plan,
        IReadOnlyCollection<Guid> previousRoundSpeakerIds,
        Guid? latestSpeakerId,
        IReadOnlyDictionary<(Guid FromId, Guid ToId), AiRelationship>
            relationships)
    {
        List<double> groupScores = plan.MemberAiAccountIds
            .Where(id => id != candidateId)
            .Select(otherId => GetMutualScore(
                candidateId,
                otherId,
                relationships))
            .ToList();
        double averageGroupScore = groupScores.Count == 0
            ? 0
            : groupScores.Average();
        double latestSpeakerScore = latestSpeakerId is Guid latestId
            && latestId != candidateId
                ? GetMutualScore(candidateId, latestId, relationships)
                : averageGroupScore;
        double repetitionPenalty = previousRoundSpeakerIds.Contains(candidateId)
            ? 28
            : 0;
        double immediateRepeatPenalty = candidateId == latestSpeakerId
            ? 18
            : 0;
        double initiatorAdjustment = candidateId == plan.InitiatorAiAccountId
            ? 5
            : 0;

        return averageGroupScore * 0.6
            + latestSpeakerScore * 0.4
            + initiatorAdjustment
            - repetitionPenalty
            - immediateRepeatPenalty;
    }

    private static double GetMutualScore(
        Guid firstId,
        Guid secondId,
        IReadOnlyDictionary<(Guid FromId, Guid ToId), AiRelationship>
            relationships)
    {
        AiRelationship firstToSecond = relationships.GetValueOrDefault(
                (firstId, secondId))
            ?? new AiRelationship(firstId, secondId);
        AiRelationship secondToFirst = relationships.GetValueOrDefault(
                (secondId, firstId))
            ?? new AiRelationship(secondId, firstId);

        return AiRelationshipScoring.CalculateMutual(
            AiRelationshipScoring.Calculate(firstToSecond),
            AiRelationshipScoring.Calculate(secondToFirst));
    }
}

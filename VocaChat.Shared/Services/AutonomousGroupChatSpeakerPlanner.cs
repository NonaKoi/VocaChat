using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 从已冻结的自主好友群聊成员中选择发起者和最多两位关系更合适的回应者。
/// </summary>
public sealed class AutonomousGroupChatSpeakerPlanner
{
    private const int MaximumResponderCount = 2;
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousGroupChatSpeakerPlanner(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public IReadOnlyList<Guid> Plan(AutonomousGroupChatPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        IReadOnlyList<Guid> responderIds = plan.MemberAiAccountIds
            .Where(id => id != plan.InitiatorAiAccountId)
            .ToList()
            .AsReadOnly();
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        Dictionary<(Guid FromId, Guid ToId), AiRelationship> relationships =
            dbContext.AiRelationships
                .AsNoTracking()
                .Where(relationship =>
                    (relationship.FromAiAccountId == plan.InitiatorAiAccountId
                        && responderIds.Contains(relationship.ToAiAccountId))
                    || (relationship.ToAiAccountId == plan.InitiatorAiAccountId
                        && responderIds.Contains(relationship.FromAiAccountId)))
                .ToDictionary(relationship => (
                    relationship.FromAiAccountId,
                    relationship.ToAiAccountId));

        List<Guid> speakers = new() { plan.InitiatorAiAccountId };
        speakers.AddRange(responderIds
            .Select((id, index) => new
            {
                Id = id,
                OriginalIndex = index,
                Score = GetMutualScore(
                    plan.InitiatorAiAccountId,
                    id,
                    relationships)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.OriginalIndex)
            .Take(MaximumResponderCount)
            .Select(item => item.Id));

        return speakers.AsReadOnly();
    }

    private static double GetMutualScore(
        Guid initiatorId,
        Guid responderId,
        IReadOnlyDictionary<(Guid FromId, Guid ToId), AiRelationship>
            relationships)
    {
        AiRelationship initiatorToResponder = relationships.GetValueOrDefault(
                (initiatorId, responderId))
            ?? new AiRelationship(initiatorId, responderId);
        AiRelationship responderToInitiator = relationships.GetValueOrDefault(
                (responderId, initiatorId))
            ?? new AiRelationship(responderId, initiatorId);

        return AiRelationshipScoring.CalculateMutual(
            AiRelationshipScoring.Calculate(initiatorToResponder),
            AiRelationshipScoring.Calculate(responderToInitiator));
    }
}

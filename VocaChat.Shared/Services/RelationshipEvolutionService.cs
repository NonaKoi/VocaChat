using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 在自主私信完成后，以小幅、幂等且可审计的方式更新两个方向的关系。
/// </summary>
public sealed class RelationshipEvolutionService
{
    private const string CompletedInteractionReason =
        "完成一次好友自主私信，双方熟悉度小幅增加。";

    private readonly VocaChatDbContextFactory _dbContextFactory;

    public RelationshipEvolutionService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 为一个已经正常完成且至少完成一轮的 Session 应用基础关系变化。
    /// 同一 Session 的同一方向只会应用一次。
    /// </summary>
    public RelationshipEvolutionStatus TryApplyCompletedSession(
        Guid sessionId,
        out IReadOnlyList<AiRelationshipChange> changes,
        out string errorMessage)
    {
        changes = Array.Empty<AiRelationshipChange>();
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        AutonomousPrivateChatSession? session =
            dbContext.AutonomousPrivateChatSessions
                .AsNoTracking()
                .SingleOrDefault(item => item.Id == sessionId);

        if (session is null)
        {
            errorMessage = "自主私信 Session 不存在，不能更新关系。";
            return RelationshipEvolutionStatus.SessionNotFound;
        }

        if (session.Status != AutonomousPrivateChatSessionStatus.Completed)
        {
            errorMessage = "只有正常完成的自主私信才能更新关系。";
            return RelationshipEvolutionStatus.SessionNotCompleted;
        }

        if (session.CompletedRounds == 0)
        {
            errorMessage = "自主私信没有完成任何普通轮，不能更新关系。";
            return RelationshipEvolutionStatus.SessionHasNoCompletedRounds;
        }

        List<AiRelationshipChange> existingChanges =
            dbContext.AiRelationshipChanges
                .AsNoTracking()
                .Where(change => change.SessionId == sessionId)
                .OrderBy(change => change.FromAiAccountId)
                .ToList();

        if (existingChanges.Count > 0)
        {
            changes = existingChanges.AsReadOnly();
            if (HasExpectedDirections(existingChanges, session))
            {
                errorMessage = string.Empty;
                return RelationshipEvolutionStatus.AlreadyApplied;
            }

            errorMessage = "自主私信的关系变化审计不完整，未重复修改关系。";
            return RelationshipEvolutionStatus.InvalidAuditState;
        }

        DateTime occurredAt = session.EndedAt ?? session.LastActivityAt;
        List<AiRelationshipChange> newChanges =
        [
            ApplyDirection(
                dbContext,
                session,
                session.InitiatorAiAccountId,
                session.RecipientAiAccountId,
                occurredAt),
            ApplyDirection(
                dbContext,
                session,
                session.RecipientAiAccountId,
                session.InitiatorAiAccountId,
                occurredAt)
        ];

        dbContext.AiRelationshipChanges.AddRange(newChanges);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException)
        {
            errorMessage = "关系演化结果暂时无法保存，请稍后重试。";
            return RelationshipEvolutionStatus.PersistenceFailed;
        }

        changes = newChanges.AsReadOnly();
        errorMessage = string.Empty;
        return RelationshipEvolutionStatus.Success;
    }

    private static AiRelationshipChange ApplyDirection(
        VocaChatDbContext dbContext,
        AutonomousPrivateChatSession session,
        Guid fromAiAccountId,
        Guid toAiAccountId,
        DateTime occurredAt)
    {
        AiRelationship relationship = dbContext.AiRelationships
            .SingleOrDefault(item =>
                item.FromAiAccountId == fromAiAccountId
                && item.ToAiAccountId == toAiAccountId)
            ?? new AiRelationship(fromAiAccountId, toAiAccountId);

        if (dbContext.Entry(relationship).State == EntityState.Detached)
        {
            dbContext.AiRelationships.Add(relationship);
        }

        (int familiarityDelta, int affinityDelta, int trustDelta) =
            relationship.ApplySessionOutcome(
                familiarityDelta: 1,
                affinityDelta: 0,
                trustDelta: 0,
                occurredAt);

        return new AiRelationshipChange(
            session.Id,
            fromAiAccountId,
            toAiAccountId,
            familiarityDelta,
            affinityDelta,
            trustDelta,
            CompletedInteractionReason,
            occurredAt);
    }

    private static bool HasExpectedDirections(
        IReadOnlyCollection<AiRelationshipChange> changes,
        AutonomousPrivateChatSession session)
    {
        return changes.Count == 2
            && changes.Any(change =>
                change.FromAiAccountId == session.InitiatorAiAccountId
                && change.ToAiAccountId == session.RecipientAiAccountId)
            && changes.Any(change =>
                change.FromAiAccountId == session.RecipientAiAccountId
                && change.ToAiAccountId == session.InitiatorAiAccountId);
    }
}

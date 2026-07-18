using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 根据点名、最近发言、档案标签和成员关系制定一至两人的群聊回复计划。
/// </summary>
public sealed class GroupChatReplyPlanner
{
    private const int MaximumReplyCount = 2;
    private const int RecentAiMessageCount = 8;
    private const double BaseFollowUpChance = 0.20;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    public GroupChatReplyPlanner(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 使用本轮随机值制定生产环境回复计划。
    /// </summary>
    public GroupChatReplyPlan CreatePlan(
        GroupChat groupChat,
        string userContent)
    {
        return CreatePlan(groupChat, userContent, Random.Shared.NextDouble());
    }

    /// <summary>
    /// 使用指定的跟进判定值制定计划，供规则测试稳定覆盖边界。
    /// </summary>
    internal GroupChatReplyPlan CreatePlan(
        GroupChat groupChat,
        string userContent,
        double followUpRoll)
    {
        ArgumentNullException.ThrowIfNull(groupChat);

        if (groupChat.Members.Count == 0)
        {
            return new GroupChatReplyPlan(
                Array.Empty<GroupChatReplyCandidate>(),
                AiSpeakerSelectionStatus.NotAttempted);
        }

        List<(AiAccount Member, int MentionIndex)> mentionedMembers =
            groupChat.Members
                .Select(member => (
                    Member: member,
                    MentionIndex: userContent.IndexOf(
                        $"@{member.Nickname}",
                        StringComparison.OrdinalIgnoreCase)))
                .Where(item => item.MentionIndex >= 0)
                .OrderBy(item => item.MentionIndex)
                .ThenBy(item => item.Member.Id)
                .Take(MaximumReplyCount)
                .ToList();

        if (mentionedMembers.Count > 0)
        {
            List<GroupChatReplyCandidate> mentionedCandidates =
                mentionedMembers
                    .Select((item, index) => new GroupChatReplyCandidate(
                        item.Member,
                        index == 0
                            ? GroupChatReplyRole.Primary
                            : GroupChatReplyRole.FollowUp,
                        100 - index))
                    .ToList();
            return new GroupChatReplyPlan(
                mentionedCandidates.AsReadOnly(),
                AiSpeakerSelectionStatus.MentionMatched);
        }

        AiSpeakerSelectionStatus selectionStatus = userContent.Contains('@')
            ? AiSpeakerSelectionStatus.MentionNotMatched
            : AiSpeakerSelectionStatus.DefaultSelection;
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        List<Guid> memberIds = groupChat.Members
            .Select(member => member.Id)
            .ToList();
        List<Guid> recentSpeakerIds = dbContext.GroupMessages
            .AsNoTracking()
            .Where(message =>
                message.GroupChatId == groupChat.Id
                && message.SenderType == MessageSenderType.AiAccount
                && message.SenderAiAccountId != null)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Take(RecentAiMessageCount)
            .Select(message => message.SenderAiAccountId!.Value)
            .ToList();
        Dictionary<Guid, List<string>> tagsByAccountId = dbContext.AiAccountTags
            .AsNoTracking()
            .Where(tag => memberIds.Contains(tag.AiAccountId))
            .GroupBy(tag => tag.AiAccountId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(tag => tag.Value).ToList());

        AiAccount primarySpeaker = groupChat.Members
            .Select(member => new
            {
                Member = member,
                Score = CalculatePrimaryScore(
                    member,
                    userContent,
                    recentSpeakerIds,
                    tagsByAccountId.GetValueOrDefault(member.Id))
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Member.CreatedAt)
            .ThenBy(candidate => candidate.Member.Id)
            .First()
            .Member;
        double primaryScore = CalculatePrimaryScore(
            primarySpeaker,
            userContent,
            recentSpeakerIds,
            tagsByAccountId.GetValueOrDefault(primarySpeaker.Id));
        List<GroupChatReplyCandidate> candidates =
        [
            new GroupChatReplyCandidate(
                primarySpeaker,
                GroupChatReplyRole.Primary,
                primaryScore)
        ];

        if (groupChat.Members.Count > 1)
        {
            TryAddFollowUpCandidate(
                dbContext,
                groupChat,
                primarySpeaker,
                userContent,
                recentSpeakerIds,
                tagsByAccountId,
                Math.Clamp(followUpRoll, 0, 1),
                candidates);
        }

        return new GroupChatReplyPlan(candidates.AsReadOnly(), selectionStatus);
    }

    private static void TryAddFollowUpCandidate(
        VocaChatDbContext dbContext,
        GroupChat groupChat,
        AiAccount primarySpeaker,
        string userContent,
        IReadOnlyList<Guid> recentSpeakerIds,
        IReadOnlyDictionary<Guid, List<string>> tagsByAccountId,
        double followUpRoll,
        ICollection<GroupChatReplyCandidate> candidates)
    {
        List<Guid> followUpMemberIds = groupChat.Members
            .Where(member => member.Id != primarySpeaker.Id)
            .Select(member => member.Id)
            .ToList();
        List<AiRelationship> relationships = dbContext.AiRelationships
            .AsNoTracking()
            .Where(relationship =>
                relationship.ToAiAccountId == primarySpeaker.Id
                && followUpMemberIds.Contains(relationship.FromAiAccountId))
            .ToList();
        Dictionary<Guid, AiRelationship> relationshipByCandidateId =
            relationships.ToDictionary(
                relationship => relationship.FromAiAccountId);

        var followUpCandidate = groupChat.Members
            .Where(member => member.Id != primarySpeaker.Id)
            .Select(member =>
            {
                AiRelationship? relationship = relationshipByCandidateId
                    .GetValueOrDefault(member.Id);
                double relationshipScore = CalculateRelationshipScore(
                    relationship);
                double score = relationshipScore
                    + CalculateTopicScore(
                        member,
                        userContent,
                        tagsByAccountId.GetValueOrDefault(member.Id))
                    + CalculateRecentSpeechPenalty(member.Id, recentSpeakerIds)
                    + CalculateStableJitter(member.Id, userContent);
                return new { Member = member, RelationshipScore = relationshipScore, Score = score };
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Member.CreatedAt)
            .ThenBy(candidate => candidate.Member.Id)
            .First();
        double followUpChance = Math.Clamp(
            BaseFollowUpChance + followUpCandidate.RelationshipScore / 200,
            BaseFollowUpChance,
            0.70);

        if (followUpRoll < followUpChance)
        {
            candidates.Add(new GroupChatReplyCandidate(
                followUpCandidate.Member,
                GroupChatReplyRole.FollowUp,
                followUpCandidate.Score));
        }
    }

    private static double CalculatePrimaryScore(
        AiAccount member,
        string userContent,
        IReadOnlyList<Guid> recentSpeakerIds,
        IReadOnlyList<string>? tags)
    {
        return CalculateTopicScore(member, userContent, tags)
            + CalculateRecentSpeechPenalty(member.Id, recentSpeakerIds)
            + CalculateStableJitter(member.Id, userContent);
    }

    private static double CalculateTopicScore(
        AiAccount member,
        string userContent,
        IReadOnlyList<string>? tags)
    {
        double score = 0;

        foreach (string tag in tags ?? Array.Empty<string>())
        {
            if (userContent.Contains(tag, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }
        }

        if (!string.IsNullOrWhiteSpace(member.Occupation)
            && userContent.Contains(
                member.Occupation,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        return Math.Min(score, 55);
    }

    private static double CalculateRecentSpeechPenalty(
        Guid memberId,
        IReadOnlyList<Guid> recentSpeakerIds)
    {
        double penalty = recentSpeakerIds.Count(id => id == memberId) * -8;

        if (recentSpeakerIds.FirstOrDefault() == memberId)
        {
            penalty -= 55;
        }

        return penalty;
    }

    private static double CalculateRelationshipScore(
        AiRelationship? relationship)
    {
        int familiarity = relationship?.Familiarity
            ?? AiRelationship.DefaultFamiliarity;
        int affinity = relationship?.Affinity
            ?? AiRelationship.DefaultAffinity;
        int trust = relationship?.Trust
            ?? AiRelationship.DefaultTrust;
        double normalizedAffinity = (affinity + 100) / 2d;

        return familiarity * 0.3
            + normalizedAffinity * 0.4
            + trust * 0.3;
    }

    private static double CalculateStableJitter(Guid memberId, string content)
    {
        uint hash = 2166136261;

        foreach (byte value in memberId.ToByteArray())
        {
            hash = (hash ^ value) * 16777619;
        }

        foreach (char value in content)
        {
            hash = (hash ^ value) * 16777619;
        }

        return hash % 1001 / 100d - 5;
    }
}

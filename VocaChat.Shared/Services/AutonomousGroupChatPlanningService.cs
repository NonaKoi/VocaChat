using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 为一个已经获准的自主好友群聊冻结成员、发起者和话题计划。
/// </summary>
public sealed class AutonomousGroupChatPlanningService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousGroupChatPlanningService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public bool TryCreatePlan(
        AutonomousGroupChatDecision decision,
        string? requestedTopic,
        out AutonomousGroupChatPlan? plan,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(decision);
        plan = null;

        if (!decision.IsApproved || decision.InitiatorAiAccountId is null)
        {
            errorMessage = "只有已经通过判断的成员组合才能建立自主好友群聊计划。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        List<AiAccount> participants = dbContext.AiAccounts
            .AsNoTracking()
            .Include(account => account.Tags)
            .Where(account =>
                decision.ParticipantAiAccountIds.Contains(account.Id))
            .ToList();

        if (participants.Count != decision.ParticipantAiAccountIds.Count)
        {
            errorMessage = "自主好友群聊的成员已经不存在。";
            return false;
        }

        string topic = ResolveTopic(
            participants,
            decision.InitiatorAiAccountId.Value,
            requestedTopic);
        AutonomousInteractionSettings settings =
            dbContext.AutonomousInteractionSettings
                .AsNoTracking()
                .SingleOrDefault(item =>
                    item.Id == AutonomousInteractionSettings.SingletonId)
            ?? new AutonomousInteractionSettings();

        if (topic.Length > AutonomousGroupChatPlan.TopicMaxLength)
        {
            errorMessage =
                $"自主好友群聊话题不能超过 {AutonomousGroupChatPlan.TopicMaxLength} 个字符。";
            return false;
        }

        plan = new AutonomousGroupChatPlan
        {
            MemberAiAccountIds = decision.ParticipantAiAccountIds,
            InitiatorAiAccountId = decision.InitiatorAiAccountId.Value,
            Topic = topic,
            IncludesLocalUser = false,
            MaximumRounds = settings.GroupChatMaximumRounds,
            ContinuationRatePercent =
                settings.GroupChatContinuationRatePercent,
            Decision = decision
        };
        errorMessage = string.Empty;
        return true;
    }

    private static string ResolveTopic(
        IReadOnlyList<AiAccount> participants,
        Guid initiatorId,
        string? requestedTopic)
    {
        if (!string.IsNullOrWhiteSpace(requestedTopic))
        {
            return requestedTopic.Trim();
        }

        List<(string Value, Guid AccountId)> interests = participants
            .SelectMany(account => account.Tags
                .Where(tag => tag.Type == AiAccountTagType.Interest)
                .Select(tag => (tag.Value, account.Id)))
            .ToList();
        string? sharedInterest = interests
            .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Value = group.First().Value,
                ParticipantCount = group
                    .Select(item => item.AccountId)
                    .Distinct()
                    .Count(),
                InitiatorHasInterest = group.Any(item =>
                    item.AccountId == initiatorId)
            })
            .Where(item => item.ParticipantCount >= 2)
            .OrderByDescending(item => item.ParticipantCount)
            .ThenByDescending(item => item.InitiatorHasInterest)
            .ThenBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Value)
            .FirstOrDefault();

        return sharedInterest
            ?? interests.FirstOrDefault(item => item.AccountId == initiatorId)
                .Value
            ?? "最近的生活";
    }
}

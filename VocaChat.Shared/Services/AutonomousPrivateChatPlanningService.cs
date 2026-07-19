using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 读取当前设置和双向关系，为一次已经获准的自主私信建立稳定计划快照。
/// </summary>
public sealed class AutonomousPrivateChatPlanningService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AutonomousPrivateChatPlanningService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public bool TryCreatePlan(
        AiAccount initiator,
        AiAccount recipient,
        string? requestedTopic,
        out AutonomousPrivateChatPlan? plan,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(initiator);
        ArgumentNullException.ThrowIfNull(recipient);
        plan = null;

        string topic = ResolveTopic(initiator, recipient, requestedTopic);
        if (topic.Length > AutonomousPrivateChatSession.TopicMaxLength)
        {
            errorMessage = $"自主私信话题不能超过 {AutonomousPrivateChatSession.TopicMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousInteractionSettings settings =
            dbContext.AutonomousInteractionSettings
                .AsNoTracking()
                .SingleOrDefault(item =>
                    item.Id == AutonomousInteractionSettings.SingletonId)
            ?? new AutonomousInteractionSettings();

        if (!settings.IsEnabled || !settings.AllowPrivateChats)
        {
            errorMessage = "当前设置不允许好友自主发起私信。";
            return false;
        }

        AiAccountAutonomySettings initiatorSettings =
            dbContext.AiAccountAutonomySettings
                .AsNoTracking()
                .SingleOrDefault(item => item.AiAccountId == initiator.Id)
            ?? new AiAccountAutonomySettings(initiator.Id);
        AiAccountAutonomySettings recipientSettings =
            dbContext.AiAccountAutonomySettings
                .AsNoTracking()
                .SingleOrDefault(item => item.AiAccountId == recipient.Id)
            ?? new AiAccountAutonomySettings(recipient.Id);

        if (!initiatorSettings.IsEnabled
            || !recipientSettings.IsEnabled
            || !initiatorSettings.CanInitiatePrivateChats)
        {
            errorMessage = "当前好友专有设置不允许本次自主私信。";
            return false;
        }

        AiRelationship initiatorToRecipient =
            dbContext.AiRelationships
                .AsNoTracking()
                .SingleOrDefault(item =>
                    item.FromAiAccountId == initiator.Id
                    && item.ToAiAccountId == recipient.Id)
            ?? new AiRelationship(initiator.Id, recipient.Id);
        AiRelationship recipientToInitiator =
            dbContext.AiRelationships
                .AsNoTracking()
                .SingleOrDefault(item =>
                    item.FromAiAccountId == recipient.Id
                    && item.ToAiAccountId == initiator.Id)
            ?? new AiRelationship(recipient.Id, initiator.Id);
        double initiatorScore =
            AiRelationshipScoring.Calculate(
                initiatorToRecipient);
        double recipientScore =
            AiRelationshipScoring.Calculate(
                recipientToInitiator);

        plan = new AutonomousPrivateChatPlan
        {
            Topic = topic,
            MaximumRounds = settings.PrivateChatMaximumRounds,
            ContinuationRatePercent =
                settings.PrivateChatContinuationRatePercent,
            InitiatorToRecipientRelationshipScore = initiatorScore,
            RecipientToInitiatorRelationshipScore = recipientScore,
            MutualRelationshipScore =
                AiRelationshipScoring.CalculateMutual(
                    initiatorScore,
                    recipientScore),
            InitiatorInitiativeLevel = initiatorSettings.InitiativeLevel
        };
        errorMessage = string.Empty;
        return true;
    }

    private static string ResolveTopic(
        AiAccount initiator,
        AiAccount recipient,
        string? requestedTopic)
    {
        if (!string.IsNullOrWhiteSpace(requestedTopic))
        {
            return requestedTopic.Trim();
        }

        HashSet<string> recipientInterests = recipient.Tags
            .Where(tag => tag.Type == AiAccountTagType.Interest)
            .Select(tag => tag.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? commonInterest = initiator.Tags
            .Where(tag => tag.Type == AiAccountTagType.Interest)
            .Select(tag => tag.Value)
            .FirstOrDefault(recipientInterests.Contains);

        return commonInterest
            ?? initiator.Tags
                .FirstOrDefault(tag => tag.Type == AiAccountTagType.Interest)
                ?.Value
            ?? "最近的生活";
    }
}

using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 读取全局或好友专有设置，并根据最近消息计算同一好友连续使用疑问句的轮数。
/// </summary>
public sealed class ConversationQuestionPolicyService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public ConversationQuestionPolicyService(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public ConversationQuestionPolicy CreatePolicy(
        Guid speakerAiAccountId,
        IReadOnlyList<AiDialogueMessage> recentMessages)
    {
        ArgumentNullException.ThrowIfNull(recentMessages);

        int maximumTurns = ResolveMaximumConsecutiveQuestionTurns(
            speakerAiAccountId);
        int consecutiveTurns = CountConsecutiveQuestionTurns(
            speakerAiAccountId,
            recentMessages);
        return new ConversationQuestionPolicy(consecutiveTurns, maximumTurns);
    }

    internal static int CountConsecutiveQuestionTurns(
        Guid speakerAiAccountId,
        IReadOnlyList<AiDialogueMessage> recentMessages)
    {
        int index = recentMessages.Count - 1;
        int streak = 0;

        while (index >= 0)
        {
            while (index >= 0
                   && recentMessages[index].SenderAiAccountId
                       != speakerAiAccountId)
            {
                index--;
            }

            if (index < 0)
            {
                break;
            }

            bool turnEndsWithQuestion = EndsWithQuestion(
                recentMessages[index].Content);
            while (index >= 0
                   && recentMessages[index].SenderAiAccountId
                       == speakerAiAccountId)
            {
                index--;
            }

            if (!turnEndsWithQuestion)
            {
                break;
            }

            streak++;
        }

        return streak;
    }

    internal static bool EndsWithQuestion(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        ReadOnlySpan<char> trimmed = content.AsSpan().TrimEnd();
        while (!trimmed.IsEmpty
               && trimmed[^1] is '”' or '’' or '"' or '\'' or ')' or '）'
                   or ']' or '】')
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        return !trimmed.IsEmpty && trimmed[^1] is '?' or '？';
    }

    private int ResolveMaximumConsecutiveQuestionTurns(
        Guid speakerAiAccountId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiAccountAutonomySettings? accountSettings = dbContext
            .AiAccountAutonomySettings
            .AsNoTracking()
            .SingleOrDefault(settings =>
                settings.AiAccountId == speakerAiAccountId);

        if (accountSettings is not null
            && !accountSettings.UseGlobalQuestionPolicy)
        {
            return accountSettings.MaximumConsecutiveQuestionTurns;
        }

        return dbContext.AutonomousInteractionSettings
            .AsNoTracking()
            .Where(settings =>
                settings.Id == AutonomousInteractionSettings.SingletonId)
            .Select(settings => settings.MaximumConsecutiveQuestionTurns)
            .SingleOrDefault()
            is int configured and > 0
                ? configured
                : AutonomousInteractionSettings
                    .DefaultMaximumConsecutiveQuestionTurns;
    }
}


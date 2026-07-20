namespace VocaChat.Services;

/// <summary>
/// 在普通轮停止后选择零至两位成员完成一次且仅一次的自然收束。
/// </summary>
public sealed class AutonomousGroupChatClosurePlanner
{
    private readonly AutonomousGroupChatSpeakerPlanner _speakerPlanner;

    public AutonomousGroupChatClosurePlanner(
        AutonomousGroupChatSpeakerPlanner speakerPlanner)
    {
        _speakerPlanner = speakerPlanner;
    }

    public bool LooksNaturallyClosed(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return true;
        }

        string[] closingPhrases =
        {
            "晚安", "先这样", "回头聊", "下次聊", "先忙", "再见", "散了"
        };
        return closingPhrases.Any(content.Contains);
    }

    public AutonomousGroupChatRoundPlan Plan(
        AutonomousGroupChatPlan plan,
        AutonomousGroupChatRoundPlan previousRound,
        string lastMessageContent,
        Guid? latestSpeakerId,
        double modeRoll,
        IReadOnlyList<double> messageModeRolls,
        IReadOnlyList<double> burstSizeRolls)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(previousRound);

        if (LooksNaturallyClosed(lastMessageContent))
        {
            return new AutonomousGroupChatRoundPlan();
        }

        double normalizedRoll = Math.Clamp(modeRoll, 0, 0.9999999999999999);
        bool endsWithQuestion = lastMessageContent.EndsWith('?')
            || lastMessageContent.EndsWith('？');
        int desiredSpeakerCount = GetSpeakerCount(
            plan.Decision.AverageRelationshipScore,
            endsWithQuestion,
            normalizedRoll);
        IReadOnlyList<Guid> speakerIds = _speakerPlanner.Plan(
            plan,
            previousRound.Speakers
                .Select(item => item.SpeakerAiAccountId)
                .ToHashSet(),
            latestSpeakerId,
            desiredSpeakerCount,
            requireInitiator: false);

        return AutonomousGroupChatRoundPlanner.CreateRoundPlan(
            plan,
            speakerIds,
            messageModeRolls,
            burstSizeRolls,
            allowThreeMessages: false);
    }

    private static int GetSpeakerCount(
        double averageRelationshipScore,
        bool endsWithQuestion,
        double roll)
    {
        if (endsWithQuestion)
        {
            return roll < 0.15 ? 0 : roll < 0.7 ? 1 : 2;
        }

        if (averageRelationshipScore >= 70)
        {
            return roll < 0.2 ? 0 : roll < 0.68 ? 1 : 2;
        }

        return roll < 0.4 ? 0 : roll < 0.85 ? 1 : 2;
    }
}

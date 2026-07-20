namespace VocaChat.Services;

/// <summary>
/// 决定普通轮由多少位成员发言，以及每位成员生成一至三条独立消息。
/// </summary>
public sealed class AutonomousGroupChatRoundPlanner
{
    private readonly AutonomousGroupChatSpeakerPlanner _speakerPlanner;

    public AutonomousGroupChatRoundPlanner(
        AutonomousGroupChatSpeakerPlanner speakerPlanner)
    {
        _speakerPlanner = speakerPlanner;
    }

    public AutonomousGroupChatRoundPlan Plan(
        AutonomousGroupChatPlan plan,
        int roundNumber,
        IReadOnlyCollection<Guid> previousRoundSpeakerIds,
        Guid? latestSpeakerId,
        double speakerCountRoll,
        IReadOnlyList<double> messageModeRolls,
        IReadOnlyList<double> burstSizeRolls)
    {
        ArgumentNullException.ThrowIfNull(plan);

        int desiredSpeakerCount = roundNumber == 1
            ? Math.Min(3, plan.MemberAiAccountIds.Count)
            : GetLaterRoundSpeakerCount(
                speakerCountRoll,
                plan.MemberAiAccountIds.Count);
        IReadOnlyList<Guid> speakerIds = _speakerPlanner.Plan(
            plan,
            previousRoundSpeakerIds,
            latestSpeakerId,
            desiredSpeakerCount,
            requireInitiator: roundNumber == 1);

        return CreateRoundPlan(
            plan,
            speakerIds,
            messageModeRolls,
            burstSizeRolls,
            allowThreeMessages: true);
    }

    internal static AutonomousGroupChatRoundPlan CreateRoundPlan(
        AutonomousGroupChatPlan plan,
        IReadOnlyList<Guid> speakerIds,
        IReadOnlyList<double> messageModeRolls,
        IReadOnlyList<double> burstSizeRolls,
        bool allowThreeMessages)
    {
        double burstProbability = plan.Decision.AverageRelationshipScore switch
        {
            < 40 => 0.15,
            >= 70 => 0.35,
            _ => 0.25
        };
        List<AutonomousGroupChatSpeakerPlan> speakers = new();

        for (int index = 0; index < speakerIds.Count; index++)
        {
            double modeRoll = GetRoll(messageModeRolls, index);
            double burstRoll = GetRoll(burstSizeRolls, index);
            int messageCount = modeRoll < burstProbability
                ? allowThreeMessages && burstRoll >= 0.72 ? 3 : 2
                : 1;
            speakers.Add(new AutonomousGroupChatSpeakerPlan
            {
                SpeakerAiAccountId = speakerIds[index],
                MessageCount = messageCount
            });
        }

        return new AutonomousGroupChatRoundPlan
        {
            Speakers = speakers.AsReadOnly()
        };
    }

    private static int GetLaterRoundSpeakerCount(
        double roll,
        int memberCount)
    {
        double normalizedRoll = Math.Clamp(roll, 0, 0.9999999999999999);
        int count = normalizedRoll switch
        {
            < 0.35 => 1,
            < 0.82 => 2,
            _ => 3
        };
        return Math.Min(count, memberCount);
    }

    private static double GetRoll(IReadOnlyList<double> rolls, int index)
    {
        return index < rolls.Count
            ? Math.Clamp(rolls[index], 0, 0.9999999999999999)
            : 0.5;
    }
}

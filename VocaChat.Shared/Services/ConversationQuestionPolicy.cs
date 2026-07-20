using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示一次生成前已经确定的疑问句节奏边界。
/// </summary>
public sealed record ConversationQuestionPolicy(
    int ConsecutiveQuestionTurns,
    int MaximumConsecutiveQuestionTurns)
{
    public bool ForceDeclarativeReply =>
        ConsecutiveQuestionTurns >= MaximumConsecutiveQuestionTurns;

    public ConversationActionPlan ApplyTo(ConversationActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!ForceDeclarativeReply)
        {
            return plan;
        }

        return plan with
        {
            Action = plan.Action == ConversationAction.Ask
                ? ConversationAction.Acknowledge
                : plan.Action,
            QuestionMode = ConversationQuestionMode.None
        };
    }
}


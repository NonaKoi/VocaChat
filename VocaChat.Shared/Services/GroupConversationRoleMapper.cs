namespace VocaChat.Services;

/// <summary>
/// 将群级导演确定的发言职责转换为单个发言者导演可执行的交流动作。
/// </summary>
internal static class GroupConversationRoleMapper
{
    public static ConversationActionPlan Apply(
        ConversationActionPlan baselinePlan,
        GroupConversationSpeakerPlan? speakerPlan)
    {
        if (speakerPlan is null)
        {
            return baselinePlan;
        }

        ConversationAction action = speakerPlan.Role switch
        {
            GroupConversationRole.DirectAnswer => ConversationAction.Answer,
            GroupConversationRole.Complement => ConversationAction.Share,
            GroupConversationRole.AgreeAndExtend => ConversationAction.Acknowledge,
            GroupConversationRole.Disagree => ConversationAction.Disagree,
            GroupConversationRole.React => ConversationAction.React,
            GroupConversationRole.Comfort => ConversationAction.Comfort,
            GroupConversationRole.Clarify => ConversationAction.Ask,
            GroupConversationRole.Tease => ConversationAction.Tease,
            GroupConversationRole.ShiftTopic => ConversationAction.ShiftTopic,
            GroupConversationRole.Close => ConversationAction.Close,
            _ => throw new ArgumentOutOfRangeException(nameof(speakerPlan.Role))
        };
        ConversationQuestionMode questionMode = speakerPlan.Role switch
        {
            GroupConversationRole.Clarify => ConversationQuestionMode.Natural,
            GroupConversationRole.Close => ConversationQuestionMode.None,
            _ => baselinePlan.QuestionMode
        };

        return baselinePlan with
        {
            Action = action,
            QuestionMode = questionMode
        };
    }
}

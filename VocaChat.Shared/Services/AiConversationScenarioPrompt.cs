namespace VocaChat.Services;

/// <summary>
/// 为导演和消息生成器提供同一份会话参与者与频道边界，避免私信和群聊语境漂移。
/// </summary>
internal static class AiConversationScenarioPrompt
{
    public static string GetDescription(
        AiMessageGenerationScenario scenario) =>
        scenario switch
        {
            AiMessageGenerationScenario.UserPrivateChat =>
                "好友与本地用户一对一私信",
            AiMessageGenerationScenario.GroupPrimaryReply => "群聊主要回复",
            AiMessageGenerationScenario.GroupFollowUpReply => "群聊补充回复",
            AiMessageGenerationScenario.AutonomousPrivateChat =>
                "两位好友自主一对一私信",
            AiMessageGenerationScenario.AutonomousPrivateChatClosing =>
                "两位好友自主一对一私信收束",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

    public static IReadOnlyList<string> GetBoundaryInstructions(
        AiMessageGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Scenario switch
        {
            AiMessageGenerationScenario.UserPrivateChat => new[]
            {
                $"当前频道是一对一私信，实际在场者只有本地用户和{request.Speaker.Nickname}。",
                "当前频道不是群聊，也没有第三位参与者或旁听者。",
                "可以回应用户提到的外部群聊，但不能把当前私信说成群聊，也不能凭空声称“大家”或其他人正在当前频道中。"
            },
            AiMessageGenerationScenario.AutonomousPrivateChat
                or AiMessageGenerationScenario.AutonomousPrivateChatClosing =>
                new[]
                {
                    $"当前频道是两位 AI 好友之间的一对一私信，实际在场者为{BuildAiParticipantList(request)}。",
                    "本地用户不在当前会话中，当前频道也不是群聊。",
                    "可以讨论外部群聊或其他人，但不能把他们说成当前频道中的参与者。"
                },
            AiMessageGenerationScenario.GroupPrimaryReply
                or AiMessageGenerationScenario.GroupFollowUpReply =>
                new[]
                {
                    $"当前频道是群聊，AI 参与者为{BuildAiParticipantList(request)}。",
                    "只能把明确列出的群成员和本地用户视为当前群聊参与者，不得虚构额外在场者。"
                },
            _ => throw new ArgumentOutOfRangeException(nameof(request.Scenario))
        };
    }

    private static string BuildAiParticipantList(
        AiMessageGenerationRequest request)
    {
        IEnumerable<string> names = new[] { request.Speaker.Nickname }
            .Concat(request.OtherParticipants.Select(account => account.Nickname))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join("、", names);
    }
}

using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 在群级模型导演不可用时，使用现有确定性业务规则生成可执行的群聊计划。
/// </summary>
public sealed class RuleBasedGroupConversationDirector
    : IGroupConversationDirector
{
    private readonly GroupChatReplyPlanner _replyPlanner;

    public RuleBasedGroupConversationDirector(
        GroupChatReplyPlanner replyPlanner)
    {
        _replyPlanner = replyPlanner
            ?? throw new ArgumentNullException(nameof(replyPlanner));
    }

    public Task<GroupConversationTurnPlan> CreatePlanAsync(
        GroupConversationPlanningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return request.Scenario == GroupConversationPlanningScenario.UserMessage
            ? Task.FromResult(CreateUserMessagePlan(request))
            : Task.FromResult(CreateAutonomousPlan(request));
    }

    private GroupConversationTurnPlan CreateUserMessagePlan(
        GroupConversationPlanningRequest request)
    {
        GroupMessage anchorMessage = request.AnchorMessage
            ?? throw new ArgumentException(
                "用户群聊计划缺少用户锚点消息。",
                nameof(request));

        GroupChatReplyPlan replyPlan = _replyPlanner.CreatePlan(
            request.GroupChat,
            anchorMessage.Content);
        IReadOnlyList<AiAccount> selectedSpeakers =
            replyPlan.SelectionStatus == AiSpeakerSelectionStatus.MentionMatched
                ? _replyPlanner.CreateCandidatePool(
                        request.GroupChat,
                        anchorMessage.Content,
                        request.MaximumSpeakerCount)
                    .Where(member => anchorMessage.Content.Contains(
                        $"@{member.Nickname}",
                        StringComparison.OrdinalIgnoreCase))
                    .ToList()
                    .AsReadOnly()
                : replyPlan.Candidates
                    .Select(candidate => candidate.Speaker)
                    .Take(request.MaximumSpeakerCount)
                    .ToList()
                    .AsReadOnly();
        IReadOnlyList<GroupConversationSpeakerPlan> speakers = selectedSpeakers
            .Take(request.MaximumSpeakerCount)
            .Select((speaker, index) => new GroupConversationSpeakerPlan
            {
                SpeakerAiAccountId = speaker.Id,
                ReplyTargetMessageId = anchorMessage.Id,
                Audience = GroupConversationAudience.LocalUser,
                Role = index == 0
                    ? GroupConversationRole.DirectAnswer
                    : GroupConversationRole.Complement,
                ResponseGoal = index == 0
                    ? "直接回应本地用户当前发送的群消息"
                    : "从不同角度补充当前群聊回应",
                NewContribution = index == 0
                    ? "回答用户消息中尚未处理的主要内容"
                    : $"由{speaker.Nickname}补充一个前面尚未表达的新角度",
                AvoidedRepetition = request.RecentMessages
                    .TakeLast(3)
                    .Select(message => Truncate(message.Content, 120))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .AsReadOnly()
            })
            .ToList()
            .AsReadOnly();

        GroupConversationTurnPlan plan = new()
        {
            AnchorMessageId = anchorMessage.Id,
            TopicFocus = Truncate(anchorMessage.Content, 120),
            TurnGoal = "让群成员围绕用户当前消息给出直接且不重复的回应",
            CoveredPoints = request.RecentMessages
                .Where(message => message.Id != request.AnchorMessage.Id)
                .TakeLast(3)
                .Select(message => Truncate(message.Content, 120))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly(),
            UnresolvedGoals = new[]
            {
                Truncate(anchorMessage.Content, 160)
            },
            Speakers = speakers,
            SelectionStatus = replyPlan.SelectionStatus,
            UsedRuleFallback = true
        };

        return plan;
    }

    private static GroupConversationTurnPlan CreateAutonomousPlan(
        GroupConversationPlanningRequest request)
    {
        IReadOnlyList<AiAccount> orderedMembers = ResolveAutonomousSpeakers(
            request);
        GroupMessage? anchorMessage = request.AnchorMessage;
        Guid? anchorSpeakerId = anchorMessage?.SenderAiAccountId;
        List<GroupConversationSpeakerPlan> speakers = new();

        foreach (AiAccount speaker in orderedMembers)
        {
            Guid? targetAiAccountId = anchorSpeakerId != speaker.Id
                ? anchorSpeakerId
                : speakers.LastOrDefault()?.SpeakerAiAccountId;
            GroupConversationAudience audience = targetAiAccountId is null
                ? GroupConversationAudience.WholeGroup
                : GroupConversationAudience.SpecificAiAccount;
            GroupConversationRole role = request.Scenario switch
            {
                GroupConversationPlanningScenario.AutonomousOpening
                    when speakers.Count == 0 =>
                        GroupConversationRole.ShiftTopic,
                GroupConversationPlanningScenario.AutonomousOpening =>
                    GroupConversationRole.React,
                GroupConversationPlanningScenario.AutonomousClosing
                    when speakers.Count == 0 =>
                        GroupConversationRole.Close,
                GroupConversationPlanningScenario.AutonomousClosing =>
                    GroupConversationRole.React,
                _ when speakers.Count == 0 =>
                    GroupConversationRole.AgreeAndExtend,
                _ => GroupConversationRole.Complement
            };

            speakers.Add(new GroupConversationSpeakerPlan
            {
                SpeakerAiAccountId = speaker.Id,
                ReplyTargetMessageId = anchorMessage?.Id,
                TargetAiAccountId = targetAiAccountId,
                Audience = audience,
                Role = role,
                ResponseGoal = GetAutonomousResponseGoal(
                    request.Scenario,
                    speakers.Count),
                NewContribution = GetAutonomousContribution(
                    request,
                    speaker,
                    speakers.Count),
                AvoidedRepetition = request.RecentMessages
                    .TakeLast(3)
                    .Select(message => Truncate(message.Content, 120))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .AsReadOnly()
            });
        }

        string topic = ResolveTopic(request);
        return new GroupConversationTurnPlan
        {
            AnchorMessageId = anchorMessage?.Id,
            TopicFocus = Truncate(topic, 120),
            TurnGoal = request.Scenario switch
            {
                GroupConversationPlanningScenario.AutonomousOpening =>
                    "由发起者自然引入话题，并只安排能够形成真实回应的少量成员参与",
                GroupConversationPlanningScenario.AutonomousClosing =>
                    "承接已经发生的内容自然收住对话，不引入新的问题或话题",
                _ => "承接最近的真实消息推进当前话题，避免成员轮流报到或重复复述"
            },
            CoveredPoints = request.RecentMessages
                .TakeLast(3)
                .Select(message => Truncate(message.Content, 120))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly(),
            UnresolvedGoals = request.Scenario ==
                GroupConversationPlanningScenario.AutonomousClosing
                    ? Array.Empty<string>()
                    : new[] { Truncate(topic, 160) },
            Speakers = speakers.AsReadOnly(),
            SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection,
            UsedRuleFallback = true
        };
    }

    private static IReadOnlyList<AiAccount> ResolveAutonomousSpeakers(
        GroupConversationPlanningRequest request)
    {
        Dictionary<Guid, AiAccount> members = request.GroupChat.Members
            .ToDictionary(member => member.Id);
        IEnumerable<Guid> preferredIds = request.PreferredSpeakerAiAccountIds;
        if (request.RequiredSpeakerAiAccountId is Guid requiredId)
        {
            preferredIds = new[] { requiredId }
                .Concat(preferredIds.Where(id => id != requiredId));
        }

        return preferredIds
            .Distinct()
            .Where(members.ContainsKey)
            .Take(request.MaximumSpeakerCount)
            .Select(id => members[id])
            .ToList()
            .AsReadOnly();
    }

    private static string ResolveTopic(
        GroupConversationPlanningRequest request) =>
        !string.IsNullOrWhiteSpace(request.Topic)
            ? request.Topic.Trim()
            : request.AnchorMessage?.Content.Trim()
                ?? "当前群聊话题";

    private static string GetAutonomousResponseGoal(
        GroupConversationPlanningScenario scenario,
        int speakerIndex) => scenario switch
        {
            GroupConversationPlanningScenario.AutonomousOpening
                when speakerIndex == 0 =>
                    "像平常聊天一样自然说起当前话题，不使用主持或宣布口吻",
            GroupConversationPlanningScenario.AutonomousOpening =>
                "回应发起者刚刚说出的实际内容，并提供一个不同的新反应",
            GroupConversationPlanningScenario.AutonomousClosing =>
                "自然接住最后的内容并结束，不再提出需要继续讨论的新问题",
            _ when speakerIndex == 0 =>
                "回应最近一条真实消息并向前推进当前话题",
            _ => "承接本轮前一位成员的实际表达，补充不同且必要的内容"
        };

    private static string GetAutonomousContribution(
        GroupConversationPlanningRequest request,
        AiAccount speaker,
        int speakerIndex) => request.Scenario switch
        {
            GroupConversationPlanningScenario.AutonomousOpening
                when speakerIndex == 0 =>
                    $"由{speaker.Nickname}围绕{ResolveTopic(request)}表达当下形成的看法、偏好或提议",
            GroupConversationPlanningScenario.AutonomousClosing =>
                $"由{speaker.Nickname}对已经发生的内容给出简短自然的收尾反应",
            _ => $"由{speaker.Nickname}补充一项前面尚未表达的新内容"
        };

    private static string Truncate(string value, int maximumLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? "当前群聊内容"
            : value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : $"{normalized[..(maximumLength - 1)]}…";
    }
}

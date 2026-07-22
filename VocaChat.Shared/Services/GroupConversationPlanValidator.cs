using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 在执行群级计划前重新验证成员、消息目标、发言顺序和文本边界。
/// </summary>
public sealed class GroupConversationPlanValidator
{
    private const int MaximumSummaryLength = 300;
    private const int MaximumListItemCount = 6;
    private const int MaximumListItemLength = 200;

    public bool TryValidate(
        GroupConversationPlanningRequest request,
        GroupConversationTurnPlan? plan,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (plan is null)
        {
            errorMessage = "群聊导演没有返回计划。";
            return false;
        }

        if (request.MaximumSpeakerCount <= 0
            || request.MaximumTotalMessageCount <= 0)
        {
            errorMessage = "群聊回复数量边界无效。";
            return false;
        }

        if (!Enum.IsDefined(request.Scenario))
        {
            errorMessage = "群聊规划场景无效。";
            return false;
        }

        GroupMessage? anchorMessage = request.AnchorMessage;
        bool isAutonomous = request.Scenario !=
            GroupConversationPlanningScenario.UserMessage;

        if (request.Scenario == GroupConversationPlanningScenario.UserMessage
            && anchorMessage is null)
        {
            errorMessage = "用户群聊计划必须保留本轮用户锚点消息。";
            return false;
        }

        if (request.Scenario is GroupConversationPlanningScenario
                .AutonomousContinuation
                or GroupConversationPlanningScenario.AutonomousClosing
            && anchorMessage is null)
        {
            errorMessage = "自主群聊推进或收束计划必须引用最近一条真实消息。";
            return false;
        }

        if (plan.AnchorMessageId != anchorMessage?.Id)
        {
            errorMessage = "群聊计划更换了本轮锚点消息。";
            return false;
        }

        if (!IsValidSummary(plan.TopicFocus)
            || !IsValidSummary(plan.TurnGoal))
        {
            errorMessage = "群聊计划缺少有效的话题焦点或整轮目标。";
            return false;
        }

        if (request.Scenario ==
                GroupConversationPlanningScenario.AutonomousOpening
            && !string.IsNullOrWhiteSpace(request.Topic)
            && !AiFactGroundingMatcher.HasGroundingOverlap(
                plan.TopicFocus,
                request.Topic))
        {
            errorMessage = "自主群聊开场偏离了本次预设话题。";
            return false;
        }

        if (!IsValidList(plan.CoveredPoints)
            || !IsValidList(plan.UnresolvedGoals))
        {
            errorMessage = "群聊计划的上下文摘要无效。";
            return false;
        }

        int minimumSpeakerCount = request.Scenario ==
            GroupConversationPlanningScenario.AutonomousClosing
                ? 0
                : 1;
        if (plan.Speakers.Count < minimumSpeakerCount
            || plan.Speakers.Count > request.MaximumSpeakerCount)
        {
            errorMessage =
                $"当前群聊计划必须选择 {minimumSpeakerCount} 到 {request.MaximumSpeakerCount} 位发言者。";
            return false;
        }

        if (request.RequiredSpeakerAiAccountId is Guid requiredSpeakerId)
        {
            if (!request.GroupChat.Members.Any(member =>
                    member.Id == requiredSpeakerId))
            {
                errorMessage = "群聊计划要求的发言者不属于当前群聊。";
                return false;
            }

            if (plan.Speakers.Count == 0
                || plan.Speakers[0].SpeakerAiAccountId != requiredSpeakerId)
            {
                errorMessage = "自主群聊第一位发言者必须是本次指定发起者。";
                return false;
            }
        }

        HashSet<Guid> memberIds = request.GroupChat.Members
            .Select(member => member.Id)
            .ToHashSet();
        HashSet<Guid> availableMessageIds = request.RecentMessages
            .Select(message => message.Id)
            .ToHashSet();
        if (anchorMessage is not null)
        {
            availableMessageIds.Add(anchorMessage.Id);
        }
        HashSet<Guid> speakerIds = new();
        HashSet<string> contributions = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < plan.Speakers.Count; index++)
        {
            GroupConversationSpeakerPlan speakerPlan = plan.Speakers[index];

            if (!memberIds.Contains(speakerPlan.SpeakerAiAccountId))
            {
                errorMessage = "群聊计划选择了不属于当前群聊的 AI。";
                return false;
            }

            if (!speakerIds.Add(speakerPlan.SpeakerAiAccountId))
            {
                errorMessage = "群聊计划重复选择了同一位发言者。";
                return false;
            }

            if (speakerPlan.ReplyTargetMessageId is Guid targetMessageId
                && !availableMessageIds.Contains(targetMessageId))
            {
                errorMessage = "群聊计划引用了当前群聊之外的目标消息。";
                return false;
            }

            if (speakerPlan.ReplyTargetMessageId is null
                && request.Scenario !=
                    GroupConversationPlanningScenario.AutonomousOpening)
            {
                errorMessage = "只有自主群聊开场可以没有已持久化的回复目标。";
                return false;
            }

            if (!Enum.IsDefined(speakerPlan.Audience)
                || !Enum.IsDefined(speakerPlan.Role))
            {
                errorMessage = "群聊计划包含不支持的受众或发言职责。";
                return false;
            }

            if (isAutonomous
                && speakerPlan.Audience == GroupConversationAudience.LocalUser)
            {
                errorMessage = "好友自主群聊不能把本地用户作为发言受众。";
                return false;
            }

            if (request.Scenario ==
                    GroupConversationPlanningScenario.AutonomousOpening
                && index == 0
                && (speakerPlan.Audience !=
                        GroupConversationAudience.WholeGroup
                    || speakerPlan.TargetAiAccountId is not null
                    || speakerPlan.ReplyTargetMessageId is not null))
            {
                errorMessage = "自主群聊发起者必须先面向全群自然开场，不能回应一条并不存在的新消息。";
                return false;
            }

            if (request.Scenario ==
                    GroupConversationPlanningScenario.AutonomousClosing
                && speakerPlan.Role is not (
                    GroupConversationRole.Close
                    or GroupConversationRole.React
                    or GroupConversationRole.Comfort))
            {
                errorMessage = "自主群聊收束计划不能重新扩展或转移话题。";
                return false;
            }

            if (!IsValidSummary(speakerPlan.ResponseGoal)
                || !IsValidSummary(speakerPlan.NewContribution)
                || !IsValidList(speakerPlan.AvoidedRepetition))
            {
                errorMessage = "群聊发言者计划缺少有效的回应目标或新增内容。";
                return false;
            }

            string normalizedContribution =
                speakerPlan.NewContribution.Trim();
            if (!contributions.Add(normalizedContribution))
            {
                errorMessage = "多位发言者不能承担完全相同的新增内容。";
                return false;
            }

            if (!TryValidateAudience(
                    speakerPlan,
                    index,
                    plan.Speakers,
                    request.RecentMessages,
                    memberIds,
                    out errorMessage))
            {
                return false;
            }
        }

        IReadOnlyList<Guid> requiredMentionedMemberIds = anchorMessage is null
            || request.Scenario != GroupConversationPlanningScenario.UserMessage
            ? Array.Empty<Guid>()
            : request.GroupChat
            .Members
            .Select(member => new
            {
                member.Id,
                MentionIndex = anchorMessage.Content.IndexOf(
                    $"@{member.Nickname}",
                    StringComparison.OrdinalIgnoreCase)
            })
            .Where(item => item.MentionIndex >= 0)
            .OrderBy(item => item.MentionIndex)
            .ThenBy(item => item.Id)
            .Take(request.MaximumSpeakerCount)
            .Select(item => item.Id)
            .ToList()
            .AsReadOnly();

        if (requiredMentionedMemberIds.Any(id => !speakerIds.Contains(id)))
        {
            errorMessage = "群聊计划遗漏了用户明确点名的群成员。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateAudience(
        GroupConversationSpeakerPlan speakerPlan,
        int speakerIndex,
        IReadOnlyList<GroupConversationSpeakerPlan> allSpeakers,
        IReadOnlyList<GroupMessage> recentMessages,
        IReadOnlySet<Guid> memberIds,
        out string errorMessage)
    {
        if (speakerPlan.Audience !=
            GroupConversationAudience.SpecificAiAccount)
        {
            if (speakerPlan.TargetAiAccountId is not null)
            {
                errorMessage = "非定向群聊发言不能携带目标 AI。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        if (speakerPlan.TargetAiAccountId is not Guid targetId
            || !memberIds.Contains(targetId)
            || targetId == speakerPlan.SpeakerAiAccountId)
        {
            errorMessage = "定向群聊发言缺少有效的目标 AI。";
            return false;
        }

        bool targetAlreadySpokeInPlan = allSpeakers
            .Take(speakerIndex)
            .Any(item => item.SpeakerAiAccountId == targetId);
        bool targetExistsInHistory = recentMessages.Any(message =>
            message.SenderAiAccountId == targetId);

        if (!targetAlreadySpokeInPlan && !targetExistsInHistory)
        {
            errorMessage = "群聊发言不能回应尚未发言且没有历史消息的目标 AI。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsValidSummary(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim().Length <= MaximumSummaryLength;

    private static bool IsValidList(IReadOnlyList<string>? values) =>
        values is not null
        && values.Count <= MaximumListItemCount
        && values.All(value =>
            !string.IsNullOrWhiteSpace(value)
            && value.Trim().Length <= MaximumListItemLength);
}

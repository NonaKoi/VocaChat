using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 将群级计划和执行失败转换为有限、可读且不包含提示词的互动日志。
/// </summary>
public sealed class GroupConversationDiagnosticService
{
    private readonly AiInteractionDiagnosticLogService _diagnosticLogService;

    public GroupConversationDiagnosticService(
        AiInteractionDiagnosticLogService diagnosticLogService)
    {
        _diagnosticLogService = diagnosticLogService
            ?? throw new ArgumentNullException(nameof(diagnosticLogService));
    }

    public void RecordPlan(
        GroupConversationPlanningRequest request,
        GroupConversationTurnPlan plan,
        string? rejectedModelPlanReason = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);

        string candidates = string.Join(
            "、",
            request.GroupChat.Members.Select(member => member.Nickname));
        string speakers = plan.Speakers.Count == 0
            ? "无需追加发言"
            : string.Join(
                "；",
                plan.Speakers.Select(item =>
                {
                    string speakerName = request.GroupChat.Members
                        .Single(member => member.Id == item.SpeakerAiAccountId)
                        .Nickname;
                    string targetName = ResolveTargetName(request, item);
                    return $"{speakerName}→{targetName} / {item.Role} / {item.NewContribution}";
                }));
        string fallbackReason = string.IsNullOrWhiteSpace(
            rejectedModelPlanReason)
                ? string.Empty
                : $"；模型计划未采用：{rejectedModelPlanReason.Trim()}";
        string detail =
            $"锚点：{request.AnchorMessage?.Id.ToString() ?? "自主开场"}；"
            + $"候选：{candidates}；发言安排：{speakers}；"
            + $"人数上限：{request.MaximumSpeakerCount}；"
            + $"消息上限：{request.MaximumTotalMessageCount}"
            + fallbackReason;

        _diagnosticLogService.TryRecord(
            plan.UsedRuleFallback
                ? AiInteractionDiagnosticSeverity.Warning
                : AiInteractionDiagnosticSeverity.Information,
            plan.UsedRuleFallback
                ? AiInteractionDiagnosticCode.GroupConversationPlanFallback
                : AiInteractionDiagnosticCode.GroupConversationPlanCreated,
            ResolveScenario(request.Scenario),
            null,
            request.GroupChat.Id,
            plan.UsedRuleFallback
                ? $"群聊已使用规则计划安排 {plan.Speakers.Count} 位好友发言。"
                : $"群聊已安排 {plan.Speakers.Count} 位好友发言。",
            detail,
            plan.UsedRuleFallback);
    }

    public void RecordFailure(
        AiMessageGenerationScenario scenario,
        Guid groupChatId,
        Guid? speakerAiAccountId,
        string stage,
        string detail,
        bool wasRecovered)
    {
        string normalizedStage = string.IsNullOrWhiteSpace(stage)
            ? "未知阶段"
            : stage.Trim();

        _diagnosticLogService.TryRecord(
            wasRecovered
                ? AiInteractionDiagnosticSeverity.Warning
                : AiInteractionDiagnosticSeverity.Error,
            AiInteractionDiagnosticCode.GroupConversationExecutionFailed,
            scenario,
            speakerAiAccountId,
            groupChatId,
            wasRecovered
                ? $"群聊在{normalizedStage}出现问题，已保留成功消息。"
                : $"群聊在{normalizedStage}失败。",
            detail,
            wasRecovered);
    }

    private static string ResolveTargetName(
        GroupConversationPlanningRequest request,
        GroupConversationSpeakerPlan speakerPlan)
    {
        if (speakerPlan.Audience == GroupConversationAudience.LocalUser)
        {
            return "本地用户";
        }

        if (speakerPlan.TargetAiAccountId is Guid targetAiAccountId)
        {
            return request.GroupChat.Members
                .SingleOrDefault(member => member.Id == targetAiAccountId)
                ?.Nickname ?? "指定好友";
        }

        return "全群";
    }

    private static AiMessageGenerationScenario ResolveScenario(
        GroupConversationPlanningScenario scenario)
    {
        return scenario == GroupConversationPlanningScenario.UserMessage
            ? AiMessageGenerationScenario.GroupPrimaryReply
            : AiMessageGenerationScenario.AutonomousGroupChat;
    }
}

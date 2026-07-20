using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 在不同聊天入口之间统一个人记忆召回、导演建议预验证和消息保存后的应用流程。
/// </summary>
public sealed class AiIdentityContinuityService
{
    private const int MaximumCandidateCount = 20;
    private const int MaximumRecalledCount = 6;

    private readonly AiSelfMemoryService _selfMemoryService;
    private readonly AiInteractionDiagnosticLogService _diagnosticLogService;

    public AiIdentityContinuityService(
        AiSelfMemoryService selfMemoryService,
        AiInteractionDiagnosticLogService diagnosticLogService)
    {
        _selfMemoryService = selfMemoryService
            ?? throw new ArgumentNullException(nameof(selfMemoryService));
        _diagnosticLogService = diagnosticLogService
            ?? throw new ArgumentNullException(nameof(diagnosticLogService));
    }

    /// <summary>
    /// 为当前发言账号召回少量仍然有效的个人记忆，并返回新的不可变生成请求。
    /// </summary>
    public AiMessageGenerationRequest PrepareGenerationRequest(
        AiMessageGenerationRequest request,
        DateTime? evaluatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        AiSelfMemoryOperationStatus status = _selfMemoryService
            .TryGetActiveContextMemories(
                request.Speaker.Id,
                MaximumCandidateCount,
                out IReadOnlyList<AiSelfMemory> memories,
                out _);
        if (status != AiSelfMemoryOperationStatus.Success)
        {
            return request with
            {
                RelevantSelfMemories = Array.Empty<AiConversationSelfMemory>()
            };
        }

        DateTime now = evaluatedAt ?? DateTime.Now;
        string contextText = BuildContextText(request);
        IReadOnlyList<AiConversationSelfMemory> recalled = memories
            .Where(memory =>
                memory.Status == AiSelfMemoryStatus.Active
                && (memory.ValidFrom is null || memory.ValidFrom <= now)
                && (memory.ValidUntil is null || memory.ValidUntil >= now))
            .Select(memory => new
            {
                Memory = memory,
                Relevance = GetRelevance(memory.Summary, contextText)
            })
            .OrderByDescending(item => item.Memory.IsUserLocked)
            .ThenByDescending(item =>
                item.Memory.Source == AiSelfMemorySource.User)
            .ThenByDescending(item => item.Relevance)
            .ThenByDescending(item => item.Memory.Salience)
            .ThenByDescending(item => item.Memory.UpdatedAt)
            .ThenBy(item => item.Memory.Id)
            .Take(MaximumRecalledCount)
            .Select(item => new AiConversationSelfMemory(
                item.Memory.Id,
                item.Memory.AiAccountId,
                item.Memory.Type,
                item.Memory.Summary,
                item.Memory.Source,
                item.Memory.Salience,
                item.Memory.IsUserLocked,
                item.Memory.OccurredAt,
                item.Memory.UpdatedAt))
            .ToList()
            .AsReadOnly();

        return request with { RelevantSelfMemories = recalled };
    }

    /// <summary>
    /// 清除不存在的引用 Id 和未通过业务规则的导演建议，避免生成器使用未获准事实。
    /// </summary>
    public AiIdentityContinuityPlan ValidateDirectionPlan(
        AiMessageGenerationRequest request,
        ConversationDirectionPlan directionPlan)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(directionPlan);

        HashSet<Guid> availableMemoryIds = request.RelevantSelfMemories
            .Select(memory => memory.Id)
            .ToHashSet();
        IReadOnlyList<Guid> referencedIds = directionPlan
            .ReferencedSelfMemoryIds
            .Where(availableMemoryIds.Contains)
            .Distinct()
            .ToList()
            .AsReadOnly();

        AiSelfMemoryOperationStatus status = _selfMemoryService
            .TryValidateDirectorProposals(
                request.Speaker.Id,
                directionPlan.SelfMemoryProposals,
                out AiSelfMemoryProposalValidationResult validation,
                out string errorMessage);
        if (status != AiSelfMemoryOperationStatus.Success)
        {
            validation = AiSelfMemoryProposalValidationResult.Empty;
        }

        ConversationDirectionPlan validatedPlan = directionPlan
            .WithValidatedSelfMemoryPlan(
                referencedIds,
                validation.AcceptedProposals);
        return new AiIdentityContinuityPlan(
            validatedPlan,
            validation,
            status == AiSelfMemoryOperationStatus.Success
                ? string.Empty
                : errorMessage);
    }

    /// <summary>
    /// 消息持久化之后应用建议，并写入不含提示词和消息正文的诊断摘要。
    /// </summary>
    public AiSelfMemoryProposalApplicationResult ApplyAfterMessagesSaved(
        AiMessageGenerationRequest request,
        AiIdentityContinuityPlan continuityPlan,
        Guid conversationId,
        IReadOnlyList<AiPersistedMessageEvidence> savedMessages)
    {
        AiSelfMemoryProposalApplicationResult applicationResult =
            _selfMemoryService.ApplyDirectorProposals(
                request.Speaker.Id,
                conversationId,
                continuityPlan.DirectionPlan.SelfMemoryProposals,
                savedMessages);

        bool hasDecision = request.RelevantSelfMemories.Count > 0
            || continuityPlan.Validation.Decisions.Count > 0
            || !string.IsNullOrWhiteSpace(continuityPlan.ValidationError);
        if (hasDecision)
        {
            bool persistenceFailed = applicationResult.Status ==
                    AiSelfMemoryProposalApplicationStatus.PersistenceFailed
                || !string.IsNullOrWhiteSpace(
                    continuityPlan.ValidationError);
            string referencedIds = continuityPlan.DirectionPlan
                .ReferencedSelfMemoryIds.Count == 0
                    ? "无"
                    : string.Join(",", continuityPlan.DirectionPlan
                        .ReferencedSelfMemoryIds);
            string rejectedReasons = string.Join(
                "；",
                continuityPlan.Validation.Decisions
                    .Where(decision => !decision.IsAccepted)
                    .Select(decision => decision.Reason)
                    .Distinct()
                    .Take(3));
            string detail =
                $"召回 {request.RelevantSelfMemories.Count} 条，导演引用 {referencedIds}；"
                + $"建议通过 {continuityPlan.Validation.AcceptedProposals.Count} 项，"
                + $"拒绝 {continuityPlan.Validation.Decisions.Count(decision => !decision.IsAccepted)} 项；"
                + $"实际应用 {applicationResult.AppliedCount} 项，"
                + $"幂等命中 {applicationResult.AlreadyAppliedCount} 项，"
                + $"后处理拒绝 {applicationResult.RejectedCount} 项。"
                + (string.IsNullOrWhiteSpace(rejectedReasons)
                    ? string.Empty
                    : $"拒绝原因：{rejectedReasons}")
                + (string.IsNullOrWhiteSpace(continuityPlan.ValidationError)
                    ? string.Empty
                    : $" 预验证错误：{continuityPlan.ValidationError}");

            _diagnosticLogService.TryRecord(
                persistenceFailed
                    ? AiInteractionDiagnosticSeverity.Warning
                    : AiInteractionDiagnosticSeverity.Information,
                persistenceFailed
                    ? AiInteractionDiagnosticCode.SelfMemoryPersistenceFailed
                    : AiInteractionDiagnosticCode.SelfMemoryDecision,
                request.Scenario,
                request.Speaker.Id,
                conversationId,
                persistenceFailed
                    ? "个人记忆后处理没有完全成功。"
                    : "已完成本轮个人记忆决策。",
                detail,
                wasRecovered: !persistenceFailed
                    && applicationResult.RejectedCount > 0);
        }

        return applicationResult;
    }

    private static string BuildContextText(AiMessageGenerationRequest request)
    {
        return string.Join(
            ' ',
            new[]
            {
                request.Topic,
                request.FocusContent,
                request.ReplyTarget?.Message?.Content ?? string.Empty
            }
                .Concat(request.RecentMessages
                    .TakeLast(3)
                    .Select(message => message.Content))
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int GetRelevance(string summary, string contextText)
    {
        if (string.IsNullOrWhiteSpace(contextText))
        {
            return 0;
        }

        return AiFactGroundingMatcher.HasGroundingOverlap(
            summary,
            contextText)
                ? 1
                : 0;
    }
}

public sealed record AiIdentityContinuityPlan(
    ConversationDirectionPlan DirectionPlan,
    AiSelfMemoryProposalValidationResult Validation,
    string ValidationError);

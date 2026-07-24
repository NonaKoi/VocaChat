using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 在不同聊天入口之间统一个人记忆召回、导演建议预验证和消息保存后的应用流程。
/// </summary>
public sealed class AiIdentityContinuityService
{
    private const int MaximumCandidateCount = 20;
    private const int MaximumProtectedFactCount = 8;
    private const int MaximumRelevantMemoryCount = 4;

    private readonly AiSelfMemoryService _selfMemoryService;
    private readonly IAiSelfMemorySemanticJudge _semanticJudge;
    private readonly AiInteractionDiagnosticLogService _diagnosticLogService;
    private readonly AiSelfMemoryCandidateExtractor _candidateExtractor;

    /// <summary>
    /// 为不执行模型语义判断的只读或规则测试场景提供安全构造方式；
    /// 一旦出现候选，默认保留为 Pending 而不是直接写入。
    /// </summary>
    public AiIdentityContinuityService(
        AiSelfMemoryService selfMemoryService,
        AiInteractionDiagnosticLogService diagnosticLogService)
        : this(
            selfMemoryService,
            new PendingSelfMemorySemanticJudge(),
            diagnosticLogService)
    {
    }

    public AiIdentityContinuityService(
        AiSelfMemoryService selfMemoryService,
        IAiSelfMemorySemanticJudge semanticJudge,
        AiInteractionDiagnosticLogService diagnosticLogService)
    {
        _selfMemoryService = selfMemoryService
            ?? throw new ArgumentNullException(nameof(selfMemoryService));
        _semanticJudge = semanticJudge
            ?? throw new ArgumentNullException(nameof(semanticJudge));
        _diagnosticLogService = diagnosticLogService
            ?? throw new ArgumentNullException(nameof(diagnosticLogService));
        _candidateExtractor = new AiSelfMemoryCandidateExtractor();
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
        var candidates = memories
            .Where(memory =>
                memory.Status == AiSelfMemoryStatus.Active
                && (memory.ValidFrom is null || memory.ValidFrom <= now)
                && (memory.ValidUntil is null || memory.ValidUntil >= now))
            .Select(memory => new
            {
                Memory = memory,
                Relevance = GetRelevance(memory, contextText)
            })
            .ToList()
            .AsReadOnly();

        var anchors = candidates
            .Where(item =>
                IsProtectedFact(item.Memory))
            .OrderByDescending(item => item.Memory.IsUserLocked)
            .ThenByDescending(item => GetTrustPriority(
                item.Memory.TrustLevel))
            .ThenByDescending(item => item.Memory.Salience)
            .ThenByDescending(item => item.Memory.UpdatedAt)
            .ThenBy(item => item.Memory.Id)
            .Take(MaximumProtectedFactCount)
            .ToList();
        HashSet<Guid> selectedIds = anchors
            .Select(item => item.Memory.Id)
            .ToHashSet();
        var relevant = candidates
            .Where(item => !selectedIds.Contains(item.Memory.Id))
            .OrderByDescending(item => item.Relevance)
            .ThenByDescending(item => GetTrustPriority(
                item.Memory.TrustLevel))
            .ThenByDescending(item => item.Memory.IsUserLocked)
            .ThenByDescending(item => item.Memory.Salience)
            .ThenByDescending(item => item.Memory.UpdatedAt)
            .ThenBy(item => item.Memory.Id)
            .Take(MaximumRelevantMemoryCount);
        IReadOnlyList<AiConversationSelfMemory> recalled = anchors
            .Concat(relevant)
            .Select(item => ToConversationMemory(item.Memory))
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
    public async Task<AiSelfMemoryProposalApplicationResult>
        ApplyAfterMessagesSavedAsync(
        AiMessageGenerationRequest request,
        AiIdentityContinuityPlan continuityPlan,
        Guid conversationId,
        IReadOnlyList<AiPersistedMessageEvidence> savedMessages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuityPlan);
        ArgumentNullException.ThrowIfNull(savedMessages);

        AiIdentityContinuityPlan effectivePlan = continuityPlan;
        AiSelfMemorySemanticJudgmentResult judgmentResult =
            AiSelfMemorySemanticJudgmentResult.Empty;
        string judgmentError = string.Empty;
        int extractedCandidateCount = 0;
        bool extractionAttempted =
            continuityPlan.DirectionPlan.SelfMemoryProposals.Count == 0
            && _candidateExtractor.HasPotentialCandidate(savedMessages);
        string candidateSource =
            continuityPlan.DirectionPlan.SelfMemoryProposals.Count > 0
                ? "导演"
                : "无";

        if (continuityPlan.DirectionPlan.SelfMemoryProposals.Count > 0
            || extractionAttempted)
        {
            AiSelfMemoryOperationStatus contextStatus = _selfMemoryService
                .TryGetSemanticJudgmentContext(
                    request.Speaker.Id,
                    conversationId,
                    savedMessages,
                    out AiSelfMemorySemanticContext? context,
                    out IReadOnlyList<AiPersistedMessageEvidence>
                        verifiedMessages,
                    out judgmentError);
            if (contextStatus == AiSelfMemoryOperationStatus.Success
                && context is not null)
            {
                if (effectivePlan.DirectionPlan.SelfMemoryProposals.Count == 0)
                {
                    IReadOnlyList<AiSelfMemoryProposal> extracted =
                        _candidateExtractor.Extract(
                            request,
                            context.ActiveMemories,
                            verifiedMessages);
                    extractedCandidateCount = extracted.Count;
                    if (extracted.Count > 0)
                    {
                        AiSelfMemoryOperationStatus validationStatus =
                            _selfMemoryService.TryValidateDirectorProposals(
                                request.Speaker.Id,
                                extracted,
                                out AiSelfMemoryProposalValidationResult
                                    extractionValidation,
                                out string extractionValidationError);
                        AiSelfMemoryProposalValidationResult
                            mergedValidation = MergeValidation(
                                continuityPlan.Validation,
                                extractionValidation);
                        string mergedValidationError =
                            validationStatus
                                == AiSelfMemoryOperationStatus.Success
                                ? continuityPlan.ValidationError
                                : JoinErrors(
                                    continuityPlan.ValidationError,
                                    extractionValidationError);
                        IReadOnlyList<AiSelfMemoryProposal>
                            acceptedCandidates = validationStatus
                                == AiSelfMemoryOperationStatus.Success
                                ? extractionValidation.AcceptedProposals
                                : Array.Empty<AiSelfMemoryProposal>();
                        effectivePlan = new AiIdentityContinuityPlan(
                            continuityPlan.DirectionPlan
                                .WithValidatedSelfMemoryPlan(
                                    continuityPlan.DirectionPlan
                                        .ReferencedSelfMemoryIds,
                                    acceptedCandidates),
                            mergedValidation,
                            mergedValidationError);
                        candidateSource = "保存后提取";
                    }
                }

                if (effectivePlan.DirectionPlan
                        .SelfMemoryProposals.Count > 0)
                {
                    try
                    {
                        judgmentResult = await _semanticJudge.JudgeAsync(
                            new AiSelfMemorySemanticJudgmentRequest(
                                request.Speaker,
                                context.CharacterWorldName,
                                context.CharacterWorldDescription,
                                effectivePlan.DirectionPlan
                                    .SelfMemoryProposals,
                                context.ActiveMemories,
                                verifiedMessages,
                                request.UsageCorrelation),
                            cancellationToken);
                        if (judgmentResult.UsedFallback)
                        {
                            judgmentError = judgmentResult.FallbackReason;
                        }
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        judgmentError =
                            "语义记忆判断执行失败，候选保留为待确认。";
                        judgmentResult =
                            AiSelfMemorySemanticJudgmentResult.Pending(
                                effectivePlan.DirectionPlan
                                    .SelfMemoryProposals,
                                judgmentError);
                    }
                }
            }
            else if (effectivePlan.DirectionPlan
                         .SelfMemoryProposals.Count > 0)
            {
                judgmentResult = AiSelfMemorySemanticJudgmentResult.Pending(
                    effectivePlan.DirectionPlan.SelfMemoryProposals,
                    string.IsNullOrWhiteSpace(judgmentError)
                        ? "语义记忆判断上下文不可用。"
                        : judgmentError);
            }
            else if (extractionAttempted)
            {
                effectivePlan = effectivePlan with
                {
                    ValidationError = JoinErrors(
                        effectivePlan.ValidationError,
                        string.IsNullOrWhiteSpace(judgmentError)
                            ? "保存后候选提取上下文不可用。"
                            : judgmentError)
                };
            }
        }

        AiSelfMemoryProposalApplicationResult applicationResult =
            _selfMemoryService.ApplyDirectorProposals(
                request.Speaker.Id,
                conversationId,
                effectivePlan.DirectionPlan.SelfMemoryProposals,
                judgmentResult,
                savedMessages);

        bool hasDecision = request.RelevantSelfMemories.Count > 0
            || effectivePlan.Validation.Decisions.Count > 0
            || extractionAttempted
            || !string.IsNullOrWhiteSpace(effectivePlan.ValidationError);
        if (hasDecision)
        {
            bool persistenceFailed = applicationResult.Status ==
                    AiSelfMemoryProposalApplicationStatus.PersistenceFailed
                || !string.IsNullOrWhiteSpace(
                    effectivePlan.ValidationError);
            string referencedIds = effectivePlan.DirectionPlan
                .ReferencedSelfMemoryIds.Count == 0
                    ? "无"
                    : string.Join(",", effectivePlan.DirectionPlan
                        .ReferencedSelfMemoryIds);
            string rejectedReasons = string.Join(
                "；",
                effectivePlan.Validation.Decisions
                    .Where(decision => !decision.IsAccepted)
                    .Select(decision => decision.Reason)
                    .Concat(judgmentResult.Decisions
                        .Where(decision => decision.Outcome
                            is AiSelfMemorySemanticOutcome.Reject
                                or AiSelfMemorySemanticOutcome.Pending)
                        .Select(decision => decision.Reason))
                    .Distinct()
                    .Take(3));
            string detail =
                $"召回 {request.RelevantSelfMemories.Count} 条，导演引用 {referencedIds}；"
                + $"候选来源 {candidateSource}，保存后提取 {extractedCandidateCount} 项；"
                + $"建议通过 {effectivePlan.Validation.AcceptedProposals.Count} 项，"
                + $"拒绝 {effectivePlan.Validation.Decisions.Count(decision => !decision.IsAccepted)} 项；"
                + $"实际应用 {applicationResult.AppliedCount} 项，"
                + $"幂等命中 {applicationResult.AlreadyAppliedCount} 项，"
                + $"新增 {applicationResult.AcceptedCount} 项，"
                + $"替代 {applicationResult.SupersededCount} 项，"
                + $"归档 {applicationResult.ArchivedCount} 项，"
                + $"待确认 {applicationResult.PendingCount} 项，"
                + $"后处理拒绝 {applicationResult.RejectedCount} 项。"
                + (string.IsNullOrWhiteSpace(rejectedReasons)
                    ? string.Empty
                    : $"拒绝原因：{rejectedReasons}")
                + (string.IsNullOrWhiteSpace(effectivePlan.ValidationError)
                    ? string.Empty
                    : $" 预验证错误：{effectivePlan.ValidationError}")
                + (string.IsNullOrWhiteSpace(judgmentError)
                    ? string.Empty
                    : $" 语义判断错误：{judgmentError}");

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

    private static AiSelfMemoryProposalValidationResult MergeValidation(
        AiSelfMemoryProposalValidationResult existing,
        AiSelfMemoryProposalValidationResult additional)
    {
        return new AiSelfMemoryProposalValidationResult(
            additional.AcceptedProposals,
            existing.Decisions
                .Concat(additional.Decisions)
                .ToList()
                .AsReadOnly());
    }

    private static string JoinErrors(string first, string second)
    {
        return string.Join(
            " ",
            new[] { first, second }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildContextText(AiMessageGenerationRequest request)
    {
        return string.Join(
            ' ',
            new[]
            {
                request.Topic,
                request.FocusContent,
                request.ReplyTarget?.Message?.Content ?? string.Empty,
                request.ConversationAnchor?.Content ?? string.Empty
            }
                .Concat(request.RecentMessages
                    .TakeLast(6)
                    .Select(message => message.Content))
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int GetRelevance(
        AiSelfMemory memory,
        string contextText)
    {
        if (string.IsNullOrWhiteSpace(contextText))
        {
            return 0;
        }

        int relevance = AiFactGroundingMatcher.HasGroundingOverlap(
            memory.Summary,
            contextText)
                ? 8
                : 0;
        relevance += memory.Type switch
        {
            AiSelfMemoryType.Plan
                when ContainsAny(
                    contextText,
                    "计划",
                    "准备",
                    "打算",
                    "接下来",
                    "进度") => 3,
            AiSelfMemoryType.OngoingActivity
                when ContainsAny(
                    contextText,
                    "最近",
                    "目前",
                    "正在",
                    "进度",
                    "怎么样") => 3,
            AiSelfMemoryType.Experience
                when ContainsAny(
                    contextText,
                    "上次",
                    "之前",
                    "记得",
                    "经历",
                    "去过") => 3,
            AiSelfMemoryType.Preference
                when ContainsAny(
                    contextText,
                    "喜欢",
                    "偏好",
                    "讨厌",
                    "更在意") => 3,
            _ => 0
        };
        return relevance;
    }

    private static bool ContainsAny(
        string value,
        params string[] fragments)
    {
        return fragments.Any(fragment =>
            value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetTrustPriority(AiSelfMemoryTrustLevel trustLevel)
    {
        return trustLevel switch
        {
            AiSelfMemoryTrustLevel.UserCanon => 4,
            AiSelfMemoryTrustLevel.EstablishedCanon => 3,
            AiSelfMemoryTrustLevel.SubjectiveState => 2,
            AiSelfMemoryTrustLevel.NarrativeCandidate => 1,
            _ => 0
        };
    }

    private static bool IsProtectedFact(AiSelfMemory memory)
    {
        return memory.IsUserLocked
            || memory.TrustLevel == AiSelfMemoryTrustLevel.UserCanon
            || memory.FactNature == AiSelfMemoryFactNature.Objective
                && memory.Mutability == AiSelfMemoryMutability.Immutable
                && memory.TrustLevel
                    == AiSelfMemoryTrustLevel.EstablishedCanon;
    }

    private static AiConversationSelfMemory ToConversationMemory(
        AiSelfMemory memory)
    {
        return new AiConversationSelfMemory(
            memory.Id,
            memory.AiAccountId,
            memory.Type,
            memory.Summary,
            memory.FactKey,
            memory.FactNature,
            memory.Mutability,
            memory.TrustLevel,
            memory.CharacterWorldId,
            memory.Source,
            memory.Salience,
            memory.IsUserLocked,
            memory.OccurredAt,
            memory.UpdatedAt);
    }

    private sealed class PendingSelfMemorySemanticJudge
        : IAiSelfMemorySemanticJudge
    {
        public Task<AiSelfMemorySemanticJudgmentResult> JudgeAsync(
            AiSelfMemorySemanticJudgmentRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AiSelfMemorySemanticJudgmentResult.Pending(
                    request.Proposals,
                    "当前流程没有配置语义记忆判断器。"));
        }
    }
}

public sealed record AiIdentityContinuityPlan(
    ConversationDirectionPlan DirectionPlan,
    AiSelfMemoryProposalValidationResult Validation,
    string ValidationError);

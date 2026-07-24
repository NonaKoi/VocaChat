using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示语义判断器对一项导演记忆候选给出的处理建议。
/// 业务 Service 仍会重新验证全部硬规则。
/// </summary>
public enum AiSelfMemorySemanticOutcome
{
    Accept,
    Reject,
    Supersede,
    Archive,
    Pending
}

/// <summary>
/// 保存语义判断器对一项候选的结构化判断。
/// </summary>
public sealed record AiSelfMemorySemanticDecision(
    int ProposalIndex,
    AiSelfMemorySemanticOutcome Outcome,
    Guid? TargetMemoryId,
    string FactKey,
    AiSelfMemoryFactNature FactNature,
    AiSelfMemoryMutability Mutability,
    string Reason);

/// <summary>
/// 提供一次批量记忆判断所需的最小角色、消息和现有事实上下文。
/// </summary>
public sealed record AiSelfMemorySemanticJudgmentRequest(
    AiAccount Speaker,
    string CharacterWorldName,
    string CharacterWorldDescription,
    IReadOnlyList<AiSelfMemoryProposal> Proposals,
    IReadOnlyList<AiConversationSelfMemory> ActiveMemories,
    IReadOnlyList<AiPersistedMessageEvidence> SavedMessages,
    AiModelUsageCorrelation? UsageCorrelation);

/// <summary>
/// 保存业务层从数据库读取的当前世界及有效个人记忆上下文。
/// </summary>
public sealed record AiSelfMemorySemanticContext(
    string CharacterWorldName,
    string CharacterWorldDescription,
    IReadOnlyList<AiConversationSelfMemory> ActiveMemories);

/// <summary>
/// 保存一次批量语义判断结果；模型不可用时所有候选进入 Pending。
/// </summary>
public sealed record AiSelfMemorySemanticJudgmentResult(
    IReadOnlyList<AiSelfMemorySemanticDecision> Decisions,
    bool UsedFallback,
    string FallbackReason)
{
    public static AiSelfMemorySemanticJudgmentResult Empty { get; } = new(
        Array.Empty<AiSelfMemorySemanticDecision>(),
        false,
        string.Empty);

    public static AiSelfMemorySemanticJudgmentResult Pending(
        IReadOnlyList<AiSelfMemoryProposal> proposals,
        string reason)
    {
        IReadOnlyList<AiSelfMemorySemanticDecision> decisions = proposals
            .Select((proposal, index) => new AiSelfMemorySemanticDecision(
                index,
                AiSelfMemorySemanticOutcome.Pending,
                proposal.TargetMemoryId,
                proposal.FactKey,
                proposal.FactNature,
                proposal.Mutability,
                reason))
            .ToList()
            .AsReadOnly();

        return new AiSelfMemorySemanticJudgmentResult(
            decisions,
            true,
            reason);
    }
}

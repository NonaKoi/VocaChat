using VocaChat.Models;

namespace VocaChat.Services;

public enum AiSelfMemoryProposalOperation
{
    Add,
    Update,
    Archive
}

/// <summary>
/// 表示导演针对当前实际发言账号提出的一项个人记忆变更，不直接代表数据库写入。
/// </summary>
public sealed record AiSelfMemoryProposal(
    AiSelfMemoryProposalOperation Operation,
    Guid? TargetMemoryId,
    Guid SubjectAiAccountId,
    Guid CharacterWorldId,
    AiSelfMemoryType Type,
    string FactKey,
    AiSelfMemoryFactNature FactNature,
    AiSelfMemoryMutability Mutability,
    string Summary,
    string Reason);

public sealed record AiSelfMemoryProposalDecision(
    AiSelfMemoryProposal Proposal,
    bool IsAccepted,
    string Reason);

/// <summary>
/// 保存导演建议经过业务预验证后的结果，生成器只能使用其中已接受的建议。
/// </summary>
public sealed class AiSelfMemoryProposalValidationResult
{
    public IReadOnlyList<AiSelfMemoryProposal> AcceptedProposals { get; }
    public IReadOnlyList<AiSelfMemoryProposalDecision> Decisions { get; }

    public AiSelfMemoryProposalValidationResult(
        IReadOnlyList<AiSelfMemoryProposal> acceptedProposals,
        IReadOnlyList<AiSelfMemoryProposalDecision> decisions)
    {
        AcceptedProposals = acceptedProposals;
        Decisions = decisions;
    }

    public static AiSelfMemoryProposalValidationResult Empty { get; } = new(
        Array.Empty<AiSelfMemoryProposal>(),
        Array.Empty<AiSelfMemoryProposalDecision>());
}

public enum AiSelfMemoryProposalApplicationStatus
{
    Success,
    PartialFailure,
    PersistenceFailed
}

public sealed class AiSelfMemoryProposalApplicationResult
{
    public AiSelfMemoryProposalApplicationStatus Status { get; init; }
    public int AppliedCount { get; init; }
    public int AlreadyAppliedCount { get; init; }
    public int AcceptedCount { get; init; }
    public int SupersededCount { get; init; }
    public int ArchivedCount { get; init; }
    public int PendingCount { get; init; }
    public int RejectedCount { get; init; }
    public string Message { get; init; } = string.Empty;

    public static AiSelfMemoryProposalApplicationResult Empty { get; } = new()
    {
        Status = AiSelfMemoryProposalApplicationStatus.Success
    };
}

/// <summary>
/// 表示已经成功写入聊天记录、可以作为记忆来源验证依据的 AI 消息。
/// </summary>
public sealed record AiPersistedMessageEvidence(
    Guid MessageId,
    string Content,
    DateTime SentAt);

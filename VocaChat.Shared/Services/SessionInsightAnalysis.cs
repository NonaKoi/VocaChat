using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示模型观察到的关系方向，不直接代表数据库数值变化。
/// </summary>
public enum RelationshipSignalPolarity
{
    Neutral,
    Positive,
    Negative
}

/// <summary>
/// 表示关系信号的相对强度，由业务规则映射为有界增量。
/// </summary>
public enum RelationshipSignalStrength
{
    None,
    Low,
    Medium,
    High
}

/// <summary>
/// 表示一条记忆候选的重要程度，由业务规则映射为 Salience。
/// </summary>
public enum SessionMemoryImportance
{
    Low,
    Medium,
    High
}

/// <summary>
/// 保存一条尚未持久化、且带有当前 Session 消息证据的记忆候选。
/// </summary>
public sealed record SessionMemoryCandidate(
    AiMemoryType Type,
    string Summary,
    SessionMemoryImportance Importance,
    IReadOnlyList<Guid> EvidenceMessageIds);

/// <summary>
/// 保存一个 AI 账号对另一个 AI 账号形成的单向 Session 洞察。
/// </summary>
public sealed record DirectionalSessionInsight(
    RelationshipSignalPolarity AffinityPolarity,
    RelationshipSignalStrength AffinityStrength,
    RelationshipSignalPolarity TrustPolarity,
    RelationshipSignalStrength TrustStrength,
    string Reason,
    IReadOnlyList<Guid> RelationshipEvidenceMessageIds,
    IReadOnlyList<SessionMemoryCandidate> MemoryCandidates)
{
    public static DirectionalSessionInsight Neutral(string reason) =>
        new(
            RelationshipSignalPolarity.Neutral,
            RelationshipSignalStrength.None,
            RelationshipSignalPolarity.Neutral,
            RelationshipSignalStrength.None,
            reason,
            Array.Empty<Guid>(),
            Array.Empty<SessionMemoryCandidate>());
}

/// <summary>
/// 表示一次 Session 的两个固定方向分析；模型不可自行指定参与者 Id。
/// </summary>
public sealed record SessionInsightAnalysis(
    DirectionalSessionInsight InitiatorPerspective,
    DirectionalSessionInsight RecipientPerspective,
    bool UsedFallback,
    string FallbackReason)
{
    public static SessionInsightAnalysis Fallback(string reason) =>
        new(
            DirectionalSessionInsight.Neutral(reason),
            DirectionalSessionInsight.Neutral(reason),
            true,
            reason);
}

/// <summary>
/// 为 Session 洞察分析器提供已经由业务层确定的参与者和消息。
/// </summary>
public sealed record SessionInsightAnalysisRequest(
    AutonomousPrivateChatSession Session,
    AiAccount Initiator,
    AiAccount Recipient,
    IReadOnlyList<PrivateMessage> Messages);

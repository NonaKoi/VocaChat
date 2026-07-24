namespace VocaChat.Models;

public enum AiInteractionDiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public enum AiInteractionDiagnosticCode
{
    MessageGenerationFailed,
    MessagePersistenceFailed,
    ReplyTimingFailed,
    SelfMemoryDecision,
    SelfMemoryPersistenceFailed,
    WorldKnowledgeProcessingFailed,
    GroupConversationPlanCreated,
    GroupConversationPlanFallback,
    GroupConversationExecutionFailed
}

/// <summary>
/// 保存 AI 互动过程中不适合直接展示在聊天页的诊断摘要。
/// </summary>
public sealed class AiInteractionDiagnosticLog
{
    public const int ScenarioMaxLength = 64;
    public const int SummaryMaxLength = 200;
    public const int DetailMaxLength = 1000;

    public Guid Id { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public AiInteractionDiagnosticSeverity Severity { get; private set; }
    public AiInteractionDiagnosticCode Code { get; private set; }
    public string Scenario { get; private set; } = string.Empty;
    public Guid? AiAccountId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
    public bool WasRecovered { get; private set; }

    private AiInteractionDiagnosticLog()
    {
    }

    internal AiInteractionDiagnosticLog(
        AiInteractionDiagnosticSeverity severity,
        AiInteractionDiagnosticCode code,
        string scenario,
        Guid? aiAccountId,
        Guid? conversationId,
        string summary,
        string detail,
        bool wasRecovered,
        DateTime? occurredAt = null)
    {
        Id = Guid.NewGuid();
        OccurredAt = occurredAt ?? DateTime.Now;
        Severity = severity;
        Code = code;
        Scenario = scenario.Trim();
        AiAccountId = aiAccountId;
        ConversationId = conversationId;
        Summary = summary.Trim();
        Detail = detail.Trim();
        WasRecovered = wasRecovered;
    }
}

namespace VocaChat.WebApi.Dtos.Settings;

public sealed class AiInteractionDiagnosticLogResponse
{
    public Guid Id { get; init; }
    public DateTime OccurredAt { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Scenario { get; init; } = string.Empty;
    public Guid? AiAccountId { get; init; }
    public Guid? ConversationId { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public bool WasRecovered { get; init; }
}


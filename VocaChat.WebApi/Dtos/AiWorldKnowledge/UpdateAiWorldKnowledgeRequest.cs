namespace VocaChat.WebApi.Dtos.AiWorldKnowledge;

public sealed class UpdateAiWorldKnowledgeRequest
{
    public string Summary { get; init; } = string.Empty;
    public string FactNature { get; init; } = string.Empty;
    public string Mutability { get; init; } = string.Empty;
    public int Salience { get; init; }
    public bool IsUserLocked { get; init; }
    public bool IsConfirmed { get; init; }
}

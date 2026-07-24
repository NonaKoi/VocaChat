namespace VocaChat.WebApi.Dtos.AiWorldKnowledge;

public sealed class UpdateAiWorldAwarenessRequest
{
    public string State { get; init; } = string.Empty;
    public bool IsUserLocked { get; init; }
}

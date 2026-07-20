namespace VocaChat.WebApi.Dtos.AiSelfMemories;

/// <summary>表示用户为一个 AI 账号新增个人记忆时提交的数据。</summary>
public sealed class CreateAiSelfMemoryRequest
{
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int Salience { get; set; }
    public bool IsUserLocked { get; set; }
    public DateTime? OccurredAt { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
}

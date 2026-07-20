namespace VocaChat.WebApi.Dtos.AiSelfMemories;

/// <summary>表示用户归档或恢复一条 AI 个人记忆时提交的数据。</summary>
public sealed class UpdateAiSelfMemoryStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

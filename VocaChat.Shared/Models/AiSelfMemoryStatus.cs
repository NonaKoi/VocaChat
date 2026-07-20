namespace VocaChat.Models;

/// <summary>
/// 表示一条个人记忆当前是否仍可用于生成上下文。
/// </summary>
public enum AiSelfMemoryStatus
{
    Active,
    Superseded,
    Archived
}

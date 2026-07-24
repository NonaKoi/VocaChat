namespace VocaChat.Models;

/// <summary>
/// 表示个人记忆是否允许随时间形成新版本。
/// </summary>
public enum AiSelfMemoryMutability
{
    Immutable,
    Mutable,
    Evolving,
    Ephemeral
}

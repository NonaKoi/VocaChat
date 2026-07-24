namespace VocaChat.Models;

/// <summary>
/// 表示当前账号对一条世界知识的可信判断。
/// </summary>
public enum AiWorldKnowledgeTrustLevel
{
    Unverified,
    DirectStatement,
    Corroborated,
    UserConfirmed
}

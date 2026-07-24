namespace VocaChat.Models;

/// <summary>
/// 表示个人记忆在生成上下文中的可信等级。
/// </summary>
public enum AiSelfMemoryTrustLevel
{
    UserCanon,
    EstablishedCanon,
    NarrativeCandidate,
    SubjectiveState
}

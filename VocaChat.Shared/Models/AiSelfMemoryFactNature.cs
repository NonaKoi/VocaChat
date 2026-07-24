namespace VocaChat.Models;

/// <summary>
/// 区分个人记忆表达的是客观事实、主观状态还是尚未升级为强事实的叙事。
/// </summary>
public enum AiSelfMemoryFactNature
{
    Objective,
    Subjective,
    Narrative
}

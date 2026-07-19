namespace VocaChat.Models;

/// <summary>
/// 表示一条长期记忆保存的事实类型，而不是生成时的临时情绪。
/// </summary>
public enum AiMemoryType
{
    ImportantEvent,
    Preference,
    Habit,
    Commitment,
    SharedExperience,
    PersonalFact
}

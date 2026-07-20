namespace VocaChat.Models;

/// <summary>
/// 区分 AI 账号自身长期事实的用途，避免把个人近况压缩成无结构文本。
/// </summary>
public enum AiSelfMemoryType
{
    PersonalFact,
    OngoingActivity,
    Plan,
    Experience,
    Preference
}

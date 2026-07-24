namespace VocaChat.Models;

/// <summary>
/// 表示一个 AI 对另一个对象所处世界的派生熟悉程度。
/// 该值由有效知识、独立主题和独立会话计算，不直接持久化。
/// </summary>
public enum AiWorldFamiliarityLevel
{
    Unfamiliar,
    FirstImpression,
    Learning,
    Familiar
}

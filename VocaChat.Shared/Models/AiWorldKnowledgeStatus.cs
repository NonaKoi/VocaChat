namespace VocaChat.Models;

/// <summary>
/// 表示一条世界知识当前是否有效、已被替代或已归档。
/// </summary>
public enum AiWorldKnowledgeStatus
{
    Active = 0,
    Superseded = 1,
    Archived = 2,

    /// <summary>
    /// 表示一条与当前恒定客观知识存在冲突、尚待澄清或用户确认的候选。
    /// 该值追加在现有枚举之后，避免改变已持久化状态的数值。
    /// </summary>
    ConflictCandidate = 3
}

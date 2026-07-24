namespace VocaChat.Models;

/// <summary>
/// 表示世界知识是一项客观陈述、主观看法、传闻还是尚未确认的信息。
/// </summary>
public enum AiWorldKnowledgeFactNature
{
    ObjectiveStatement,
    SubjectiveView,
    Hearsay,
    Unconfirmed
}

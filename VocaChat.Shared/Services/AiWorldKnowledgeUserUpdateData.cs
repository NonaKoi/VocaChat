using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示本地用户对一条世界知识执行的明确修订和确认。
/// </summary>
public sealed record AiWorldKnowledgeUserUpdateData(
    string Summary,
    AiWorldKnowledgeFactNature FactNature,
    AiWorldKnowledgeMutability Mutability,
    int Salience,
    bool IsUserLocked,
    bool IsConfirmed);

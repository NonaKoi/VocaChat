using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示准备从一条正式消息中保存的世界知识内容和分类。
/// </summary>
public sealed record AiWorldKnowledgeWriteData(
    Guid OwnerAiAccountId,
    Guid SubjectCharacterWorldId,
    Guid? SubjectAiAccountId,
    string KnowledgeKey,
    string Summary,
    AiWorldKnowledgeFactNature FactNature,
    AiWorldKnowledgeMutability Mutability,
    AiWorldKnowledgeTrustLevel TrustLevel,
    int Salience,
    bool IsUserLocked);

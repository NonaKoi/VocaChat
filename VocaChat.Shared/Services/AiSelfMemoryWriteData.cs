using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示用户创建或修订个人记忆时提交的事实内容与分类。
/// 缺省分类由 Service 根据现有记忆类型和账号当前世界补全。
/// </summary>
public sealed record AiSelfMemoryWriteData(
    AiSelfMemoryType Type,
    string Summary,
    int Salience,
    bool IsUserLocked,
    DateTime? OccurredAt,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    string? FactKey = null,
    AiSelfMemoryFactNature? FactNature = null,
    AiSelfMemoryMutability? Mutability = null,
    Guid? CharacterWorldId = null);

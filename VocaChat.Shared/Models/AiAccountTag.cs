using System;

namespace VocaChat.Models;

/// <summary>
/// 表示一个属于特定 AI 账号的结构化档案标签。
/// </summary>
public class AiAccountTag
{
    internal const int ValueMaxLength = 30;

    public Guid AiAccountId { get; private set; }
    public AiAccountTagType Type { get; private set; }
    public string Value { get; private set; }

    /// <summary>
    /// 供 EF Core 从数据库还原实体使用。
    /// </summary>
    private AiAccountTag()
    {
        Value = string.Empty;
    }

    internal AiAccountTag(
        Guid aiAccountId,
        AiAccountTagType type,
        string value)
    {
        AiAccountId = aiAccountId;
        Type = type;
        Value = value;
    }
}

using System;
using System.Collections.Generic;

namespace VocaChat.Models;

/// <summary>
/// 表示一个由当前本地用户创建的群聊。
/// </summary>
public class GroupChat
{
    internal const int NameMaxLength = 100;

    private readonly List<AiAccount> _members = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyList<AiAccount> Members => _members.AsReadOnly();
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// 供 EF Core 从数据库还原群聊和成员关系使用。
    /// </summary>
    private GroupChat()
    {
        Name = string.Empty;
    }

    /// <summary>
    /// 创建一个空成员的群聊。
    /// </summary>
    internal GroupChat(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
        CreatedAt = DateTime.Now;
    }

    /// <summary>
    /// 保存已经由群聊 Service 验证通过的 AI 账号成员。
    /// </summary>
    internal void AddMember(AiAccount aiAccount)
    {
        _members.Add(aiAccount);
    }
}

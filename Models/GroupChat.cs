using System;
using System.Collections.Generic;

namespace VocaChat.ConsoleApp.Models;

/// <summary>
/// 表示一个由当前本地用户创建的群聊。
/// </summary>
public class GroupChat
{
    private readonly List<AiAccount> _members = new();
    private readonly List<GroupMessage> _messages = new();

    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<AiAccount> Members => _members.AsReadOnly();
    public IReadOnlyList<GroupMessage> Messages => _messages.AsReadOnly();
    public DateTime CreatedAt { get; }

    /// <summary>
    /// 创建一个空成员的群聊，并初始化消息集合。
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

    /// <summary>
    /// 保存已经由消息 Service 验证通过的群消息。
    /// </summary>
    internal void AddMessage(GroupMessage message)
    {
        _messages.Add(message);
    }
}

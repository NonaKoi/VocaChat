using System;
using System.Collections.Generic;

namespace VocaChat.ConsoleApp.Models;

/// <summary>
/// 表示一个由当前本地用户创建的群聊。
/// </summary>
public class GroupChat
{
    public Guid Id { get; }
    public string Name { get; }
    public List<AiAccount> Members { get; }
    public List<GroupMessage> Messages { get; }
    public DateTime CreatedAt { get; }

    /// <summary>
    /// 创建群聊，保存用户选择的 AI 账号成员，并初始化空的消息集合。
    /// </summary>
    public GroupChat(string name, List<AiAccount> members)
    {
        Id = Guid.NewGuid();
        Name = name;
        Members = members;
        Messages = new List<GroupMessage>();
        CreatedAt = DateTime.Now;
    }
}

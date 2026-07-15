using System;
using System.Collections.Generic;

namespace VocaChat.WebApi.Dtos.GroupChats;

/// <summary>
/// 表示通过 HTTP 返回给客户端的群聊及其成员摘要。
/// </summary>
public sealed class GroupChatResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<GroupChatMemberResponse> Members { get; init; } = new();
}

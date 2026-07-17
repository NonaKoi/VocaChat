using System;
using System.Collections.Generic;

namespace VocaChat.WebApi.Dtos.GroupChats;

/// <summary>
/// 表示客户端创建群聊时允许提交的 HTTP 请求数据。
/// </summary>
public sealed class CreateGroupChatRequest
{
    public string? Name { get; set; }
    public List<Guid> MemberAiAccountIds { get; set; } = new();
    public bool IncludesLocalUser { get; set; } = true;
}

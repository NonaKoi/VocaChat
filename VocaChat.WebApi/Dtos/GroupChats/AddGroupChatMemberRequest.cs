using System;

namespace VocaChat.WebApi.Dtos.GroupChats;

/// <summary>
/// 表示客户端向已有群聊添加一个 AI 成员的 HTTP 请求数据。
/// </summary>
public sealed class AddGroupChatMemberRequest
{
    public Guid AiAccountId { get; set; }
}

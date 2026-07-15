using System;

namespace VocaChat.WebApi.Dtos.GroupChats;

/// <summary>
/// 表示群聊响应中的简要 AI 成员信息。
/// </summary>
public sealed class GroupChatMemberResponse
{
    public Guid Id { get; init; }
    public string Nickname { get; init; } = string.Empty;
}

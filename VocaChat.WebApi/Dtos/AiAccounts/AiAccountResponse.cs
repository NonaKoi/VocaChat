using System;

namespace VocaChat.WebApi.Dtos.AiAccounts;

/// <summary>
/// 表示通过 HTTP 返回给客户端的 AI 账号数据。
/// </summary>
public sealed class AiAccountResponse
{
    public Guid Id { get; init; }
    public string Nickname { get; init; } = string.Empty;
    public string IdentityDescription { get; init; } = string.Empty;
    public string Personality { get; init; } = string.Empty;
    public string SpeakingStyle { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

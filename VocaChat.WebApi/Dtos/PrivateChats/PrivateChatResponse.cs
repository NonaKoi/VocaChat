using VocaChat.WebApi.Dtos.AiAccounts;

namespace VocaChat.WebApi.Dtos.PrivateChats;

/// <summary>表示当前本地用户与一位好友的私聊。</summary>
public sealed class PrivateChatResponse
{
    public Guid Id { get; init; }
    public Guid ContactId { get; init; }
    public AiAccountResponse Friend { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}

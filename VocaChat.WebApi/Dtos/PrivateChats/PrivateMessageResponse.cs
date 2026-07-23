using VocaChat.WebApi.Dtos.Common;

namespace VocaChat.WebApi.Dtos.PrivateChats;

/// <summary>表示一条私聊消息。</summary>
public sealed class PrivateMessageResponse
{
    public Guid Id { get; init; }
    public Guid PrivateChatId { get; init; }
    public string SenderType { get; init; } = string.Empty;
    public string SenderDisplayName { get; init; } = string.Empty;
    public Guid? SenderAiAccountId { get; init; }
    public long SequenceNumber { get; init; }
    public AiMessageTokenUsageResponse? TokenUsage { get; init; }
    public string? SenderAvatarUrl { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
}

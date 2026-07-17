namespace VocaChat.WebApi.Dtos.Conversations;

/// <summary>表示私聊或群聊在会话列表中的最新摘要。</summary>
public sealed class ConversationSummaryResponse
{
    public string Kind { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public Guid Id { get; init; }
    public Guid? ContactId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public int MemberCount { get; init; }
    public string? LatestSenderDisplayName { get; init; }
    public string? LatestMessageContent { get; init; }
    public DateTime? LatestMessageAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

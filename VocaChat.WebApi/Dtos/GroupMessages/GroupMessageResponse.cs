using System;

namespace VocaChat.WebApi.Dtos.GroupMessages;

/// <summary>
/// 表示通过 HTTP 返回的一条群消息历史快照。
/// </summary>
public sealed class GroupMessageResponse
{
    public Guid Id { get; init; }
    public Guid GroupChatId { get; init; }
    public string SenderType { get; init; } = string.Empty;
    public string SenderDisplayName { get; init; } = string.Empty;
    public Guid? SenderAiAccountId { get; init; }
    public long SequenceNumber { get; init; }
    public Guid? InteractionBatchId { get; init; }
    public Guid? ReplyToMessageId { get; init; }
    public string? SenderAvatarUrl { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
}

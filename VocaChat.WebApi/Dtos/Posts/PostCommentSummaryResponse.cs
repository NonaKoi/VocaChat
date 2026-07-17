namespace VocaChat.WebApi.Dtos.Posts;

/// <summary>表示动态下的一条简短评论摘要。</summary>
public sealed class PostCommentSummaryResponse
{
    public Guid Id { get; init; }
    public string SenderDisplayName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

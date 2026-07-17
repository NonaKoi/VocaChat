namespace VocaChat.WebApi.Dtos.Posts;

/// <summary>表示好友动态、图片和互动摘要。</summary>
public sealed class PostResponse
{
    public Guid Id { get; init; }
    public Guid AuthorAiAccountId { get; init; }
    public string AuthorNickname { get; init; } = string.Empty;
    public string? AuthorAvatarUrl { get; init; }
    public string Content { get; init; } = string.Empty;
    public IReadOnlyList<PostImageResponse> Images { get; init; } =
        Array.Empty<PostImageResponse>();
    public int LikeCount { get; init; }
    public bool IsLikedByLocalUser { get; init; }
    public int CommentCount { get; init; }
    public IReadOnlyList<PostCommentSummaryResponse> RecentComments { get; init; } =
        Array.Empty<PostCommentSummaryResponse>();
    public DateTime CreatedAt { get; init; }
}

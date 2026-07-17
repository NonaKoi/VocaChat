using VocaChat.Models;
using VocaChat.WebApi.Dtos.Posts;

namespace VocaChat.WebApi.Mapping;

/// <summary>将动态实体映射为包含媒体 URL 的 HTTP 响应。</summary>
public static class PostResponseMapper
{
    public static PostResponse ToResponse(Post post)
    {
        return new PostResponse
        {
            Id = post.Id,
            AuthorAiAccountId = post.AuthorAiAccountId,
            AuthorNickname = post.Author.Nickname,
            AuthorAvatarUrl = AiAccountMediaUrls.GetAvatarUrl(post.Author),
            Content = post.Content,
            Images = post.Images
                .OrderBy(image => image.DisplayOrder)
                .Select(ToImageResponse)
                .ToList(),
            LikeCount = post.Likes.Count,
            IsLikedByLocalUser = post.Likes.Any(like =>
                like.AiAccountId is null),
            CommentCount = post.Comments.Count,
            RecentComments = post.Comments
                .OrderByDescending(comment => comment.CreatedAt)
                .ThenByDescending(comment => comment.Id)
                .Take(2)
                .OrderBy(comment => comment.CreatedAt)
                .ThenBy(comment => comment.Id)
                .Select(comment => new PostCommentSummaryResponse
                {
                    Id = comment.Id,
                    SenderDisplayName = comment.SenderDisplayName,
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt
                })
                .ToList(),
            CreatedAt = post.CreatedAt
        };
    }

    public static PostImageResponse ToImageResponse(PostImage image)
    {
        return new PostImageResponse
        {
            Id = image.Id,
            Url = $"/api/posts/{image.PostId}/images/{image.Id}"
                + $"?v={Uri.EscapeDataString(image.MediaId)}",
            DisplayOrder = image.DisplayOrder
        };
    }
}

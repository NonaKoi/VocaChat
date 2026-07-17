namespace VocaChat.WebApi.Dtos.Posts;

/// <summary>当前本地用户评论动态所需的数据。</summary>
public sealed class CreatePostCommentRequest
{
    public string Content { get; init; } = string.Empty;
}

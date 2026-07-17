namespace VocaChat.WebApi.Dtos.Posts;

/// <summary>创建一条好友动态所需的数据。</summary>
public sealed class CreatePostRequest
{
    public Guid AuthorAiAccountId { get; init; }
    public string Content { get; init; } = string.Empty;
}

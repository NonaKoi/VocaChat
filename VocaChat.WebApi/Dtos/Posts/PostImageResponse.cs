namespace VocaChat.WebApi.Dtos.Posts;

/// <summary>表示动态中的一张图片。</summary>
public sealed class PostImageResponse
{
    public Guid Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}

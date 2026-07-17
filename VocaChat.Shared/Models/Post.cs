namespace VocaChat.Models;

/// <summary>
/// 表示一位 AI 好友发布的动态。
/// </summary>
public class Post
{
    internal const int ContentMaxLength = 2000;

    private readonly List<PostImage> _images = new();
    private readonly List<PostLike> _likes = new();
    private readonly List<PostComment> _comments = new();

    public Guid Id { get; private set; }
    public Guid AuthorAiAccountId { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public AiAccount Author { get; private set; }
    public IReadOnlyList<PostImage> Images => _images.AsReadOnly();
    public IReadOnlyList<PostLike> Likes => _likes.AsReadOnly();
    public IReadOnlyList<PostComment> Comments => _comments.AsReadOnly();

    private Post()
    {
        Content = string.Empty;
        Author = null!;
    }

    internal Post(Guid authorAiAccountId, string content)
    {
        Id = Guid.NewGuid();
        AuthorAiAccountId = authorAiAccountId;
        Content = content;
        CreatedAt = DateTime.Now;
        Author = null!;
    }

    internal PostImage AddImage(string mediaId, int displayOrder)
    {
        PostImage image = new(Id, mediaId, displayOrder);
        _images.Add(image);
        return image;
    }

    internal PostLike AddLocalUserLike()
    {
        PostLike like = new(Id, null);
        _likes.Add(like);
        return like;
    }

    internal PostComment AddLocalUserComment(string content)
    {
        PostComment comment = new(Id, null, "我", content);
        _comments.Add(comment);
        return comment;
    }
}

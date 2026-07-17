namespace VocaChat.Models;

/// <summary>
/// 表示一次动态点赞；AiAccountId 为空时代表当前本地用户。
/// </summary>
public class PostLike
{
    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public Guid? AiAccountId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PostLike()
    {
    }

    internal PostLike(Guid postId, Guid? aiAccountId)
    {
        Id = Guid.NewGuid();
        PostId = postId;
        AiAccountId = aiAccountId;
        CreatedAt = DateTime.Now;
    }
}

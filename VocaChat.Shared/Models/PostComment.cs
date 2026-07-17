namespace VocaChat.Models;

/// <summary>
/// 表示动态下的一条评论；发送者名称作为历史快照保存。
/// </summary>
public class PostComment
{
    internal const int SenderDisplayNameMaxLength = 100;
    internal const int ContentMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public Guid? AiAccountId { get; private set; }
    public string SenderDisplayName { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PostComment()
    {
        SenderDisplayName = string.Empty;
        Content = string.Empty;
    }

    internal PostComment(
        Guid postId,
        Guid? aiAccountId,
        string senderDisplayName,
        string content)
    {
        Id = Guid.NewGuid();
        PostId = postId;
        AiAccountId = aiAccountId;
        SenderDisplayName = senderDisplayName;
        Content = content;
        CreatedAt = DateTime.Now;
    }
}

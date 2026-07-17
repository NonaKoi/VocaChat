namespace VocaChat.Models;

/// <summary>
/// 表示动态中的一张本地图片；只保存不透明媒体标识。
/// </summary>
public class PostImage
{
    internal const int MediaIdMaxLength = 80;

    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public string MediaId { get; private set; }
    public int DisplayOrder { get; private set; }

    private PostImage()
    {
        MediaId = string.Empty;
    }

    internal PostImage(Guid postId, string mediaId, int displayOrder)
    {
        Id = Guid.NewGuid();
        PostId = postId;
        MediaId = mediaId;
        DisplayOrder = displayOrder;
    }
}

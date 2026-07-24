namespace VocaChat.Models;

/// <summary>
/// 保存一条群消息写入时能够看见该消息的 AI 群成员快照。
/// 该记录表示可见性，不表示用户界面中的已读状态。
/// </summary>
public sealed class GroupMessageAudience
{
    public Guid GroupMessageId { get; private set; }
    public Guid AiAccountId { get; private set; }
    public DateTime VisibleAt { get; private set; }
    public GroupMessage GroupMessage { get; private set; }
    public AiAccount AiAccount { get; private set; }

    private GroupMessageAudience()
    {
        GroupMessage = null!;
        AiAccount = null!;
    }

    internal GroupMessageAudience(
        Guid groupMessageId,
        Guid aiAccountId,
        DateTime visibleAt)
    {
        GroupMessageId = groupMessageId;
        AiAccountId = aiAccountId;
        VisibleAt = visibleAt;
        GroupMessage = null!;
        AiAccount = null!;
    }
}

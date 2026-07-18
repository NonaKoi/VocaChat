using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示一个 AI 在本轮群聊回复中的职责。
/// </summary>
public enum GroupChatReplyRole
{
    Primary,
    FollowUp
}

/// <summary>
/// 表示本轮选中的一个群成员及其回复顺序和评分。
/// </summary>
public sealed class GroupChatReplyCandidate
{
    public AiAccount Speaker { get; }
    public GroupChatReplyRole Role { get; }
    public double Score { get; }

    internal GroupChatReplyCandidate(
        AiAccount speaker,
        GroupChatReplyRole role,
        double score)
    {
        Speaker = speaker;
        Role = role;
        Score = score;
    }
}

/// <summary>
/// 保存一轮群聊要由哪些成员、按什么顺序回复。
/// </summary>
public sealed class GroupChatReplyPlan
{
    public IReadOnlyList<GroupChatReplyCandidate> Candidates { get; }
    public AiSpeakerSelectionStatus SelectionStatus { get; }

    internal GroupChatReplyPlan(
        IReadOnlyList<GroupChatReplyCandidate> candidates,
        AiSpeakerSelectionStatus selectionStatus)
    {
        Candidates = candidates;
        SelectionStatus = selectionStatus;
    }
}

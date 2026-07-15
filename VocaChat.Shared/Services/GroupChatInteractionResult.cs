using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 表示一次“用户消息 + 模拟 AI 回复”交互执行到哪个阶段。
/// </summary>
public enum GroupChatInteractionStatus
{
    Succeeded,
    UserMessageRejected,
    AiReplyFailed
}

/// <summary>
/// 表示本轮 AI 发言者是如何选择的，供不同 Host 保留一致的提示语义。
/// </summary>
public enum AiSpeakerSelectionStatus
{
    NotAttempted,
    DefaultSelection,
    MentionMatched,
    MentionNotMatched
}

/// <summary>
/// 保存一次群聊交互的阶段、已落库消息和必要的失败信息。
/// </summary>
public sealed class GroupChatInteractionResult
{
    public GroupChatInteractionStatus Status { get; }
    public GroupMessage? UserMessage { get; }
    public GroupMessage? AiReply { get; }
    public AiSpeakerSelectionStatus SpeakerSelectionStatus { get; }
    public string ErrorMessage { get; }

    private GroupChatInteractionResult(
        GroupChatInteractionStatus status,
        GroupMessage? userMessage,
        GroupMessage? aiReply,
        AiSpeakerSelectionStatus speakerSelectionStatus,
        string errorMessage)
    {
        Status = status;
        UserMessage = userMessage;
        AiReply = aiReply;
        SpeakerSelectionStatus = speakerSelectionStatus;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 创建“用户消息未保存”的失败结果。
    /// </summary>
    internal static GroupChatInteractionResult UserMessageRejected(
        string errorMessage)
    {
        return new GroupChatInteractionResult(
            GroupChatInteractionStatus.UserMessageRejected,
            null,
            null,
            AiSpeakerSelectionStatus.NotAttempted,
            errorMessage);
    }

    /// <summary>
    /// 创建“用户消息已保存，但 AI 回复未保存”的部分失败结果。
    /// </summary>
    internal static GroupChatInteractionResult AiReplyFailed(
        GroupMessage userMessage,
        AiSpeakerSelectionStatus speakerSelectionStatus,
        string errorMessage)
    {
        return new GroupChatInteractionResult(
            GroupChatInteractionStatus.AiReplyFailed,
            userMessage,
            null,
            speakerSelectionStatus,
            errorMessage);
    }

    /// <summary>
    /// 创建用户消息和 AI 回复均已保存的成功结果。
    /// </summary>
    internal static GroupChatInteractionResult Succeeded(
        GroupMessage userMessage,
        GroupMessage aiReply,
        AiSpeakerSelectionStatus speakerSelectionStatus)
    {
        return new GroupChatInteractionResult(
            GroupChatInteractionStatus.Succeeded,
            userMessage,
            aiReply,
            speakerSelectionStatus,
            string.Empty);
    }
}

using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.GroupMessages;

namespace VocaChat.WebApi.Mapping;

/// <summary>
/// 将群消息实体映射为保留发送者显示名快照的 HTTP 响应。
/// </summary>
internal static class GroupMessageResponseMapper
{
    public static GroupMessageResponse ToResponse(
        GroupMessage message,
        IReadOnlyList<AiAccount> members,
        AiMessageTokenUsageSummary? tokenUsage = null)
    {
        AiAccount? sender = message.SenderAiAccountId is null
            ? null
            : members.SingleOrDefault(member =>
                member.Id == message.SenderAiAccountId.Value);

        return new GroupMessageResponse
        {
            Id = message.Id,
            GroupChatId = message.GroupChatId,
            SenderType = message.SenderType switch
            {
                MessageSenderType.User => "User",
                MessageSenderType.AiAccount => "AiAccount",
                _ => throw new InvalidOperationException(
                    "无法识别群消息发送者类型。")
            },
            SenderDisplayName = message.SenderDisplayName,
            SenderAiAccountId = message.SenderAiAccountId,
            SequenceNumber = message.SequenceNumber,
            InteractionBatchId = message.InteractionBatchId,
            ReplyToMessageId = message.ReplyToMessageId,
            TokenUsage = AiMessageTokenUsageResponseMapper.ToResponse(
                tokenUsage),
            SenderAvatarUrl = sender is null
                ? null
                : AiAccountMediaUrls.GetAvatarUrl(sender),
            Content = message.Content,
            SentAt = message.SentAt
        };
    }
}

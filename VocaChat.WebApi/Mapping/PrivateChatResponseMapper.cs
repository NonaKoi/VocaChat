using VocaChat.Models;
using VocaChat.WebApi.Dtos.PrivateChats;

namespace VocaChat.WebApi.Mapping;

/// <summary>将私聊和私聊消息实体映射为 HTTP 响应。</summary>
public static class PrivateChatResponseMapper
{
    public static PrivateChatResponse ToResponse(PrivateChat privateChat)
    {
        return new PrivateChatResponse
        {
            Id = privateChat.Id,
            ContactId = privateChat.ContactId,
            Friend = AiAccountResponseMapper.ToResponse(
                privateChat.Contact.AiAccount),
            CreatedAt = privateChat.CreatedAt
        };
    }

    public static PrivateMessageResponse ToMessageResponse(
        PrivateMessage message,
        AiAccount friend)
    {
        return new PrivateMessageResponse
        {
            Id = message.Id,
            PrivateChatId = message.PrivateChatId,
            SenderType = message.SenderType.ToString(),
            SenderDisplayName = message.SenderDisplayName,
            SenderAiAccountId = message.SenderAiAccountId,
            SenderAvatarUrl = message.SenderAiAccountId == friend.Id
                ? AiAccountMediaUrls.GetAvatarUrl(friend)
                : null,
            Content = message.Content,
            SentAt = message.SentAt
        };
    }
}

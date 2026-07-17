using VocaChat.Models;
using VocaChat.WebApi.Dtos.PrivateChats;

namespace VocaChat.WebApi.Mapping;

/// <summary>
/// 将私信及其消息映射为稳定的 HTTP 响应。
/// </summary>
public static class PrivateChatResponseMapper
{
    public static PrivateChatResponse ToResponse(PrivateChat privateChat)
    {
        List<AiAccount> participants = GetParticipants(privateChat);
        AiAccount? friend = privateChat.Kind
            == PrivateChatKind.LocalUserAndAiAccount
                ? participants.SingleOrDefault()
                : null;

        return new PrivateChatResponse
        {
            Id = privateChat.Id,
            Category = privateChat.Kind
                == PrivateChatKind.LocalUserAndAiAccount
                    ? ConversationCategory.MyPrivateChat.ToString()
                    : ConversationCategory.FriendPrivateChat.ToString(),
            ContactId = privateChat.ContactId,
            Friend = friend is null
                ? null
                : AiAccountResponseMapper.ToResponse(friend),
            Participants = participants
                .Select(AiAccountResponseMapper.ToResponse)
                .ToList(),
            CreatedAt = privateChat.CreatedAt
        };
    }

    public static PrivateMessageResponse ToMessageResponse(
        PrivateMessage message,
        IReadOnlyList<AiAccount> participants)
    {
        AiAccount? sender = message.SenderAiAccountId is Guid senderId
            ? participants.FirstOrDefault(account => account.Id == senderId)
            : null;

        return new PrivateMessageResponse
        {
            Id = message.Id,
            PrivateChatId = message.PrivateChatId,
            SenderType = message.SenderType.ToString(),
            SenderDisplayName = message.SenderDisplayName,
            SenderAiAccountId = message.SenderAiAccountId,
            SenderAvatarUrl = sender is null
                ? null
                : AiAccountMediaUrls.GetAvatarUrl(sender),
            Content = message.Content,
            SentAt = message.SentAt
        };
    }

    private static List<AiAccount> GetParticipants(PrivateChat privateChat)
    {
        if (privateChat.Kind == PrivateChatKind.LocalUserAndAiAccount)
        {
            return privateChat.Contact is null
                ? new List<AiAccount>()
                : new List<AiAccount> { privateChat.Contact.AiAccount };
        }

        return new[]
            {
                privateChat.FirstAiAccount,
                privateChat.SecondAiAccount
            }
            .OfType<AiAccount>()
            .ToList();
    }
}

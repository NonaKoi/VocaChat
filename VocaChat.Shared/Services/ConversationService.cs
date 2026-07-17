using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 通过数据库投影生成私聊和群聊的统一最近会话摘要。
/// </summary>
public sealed class ConversationService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public ConversationService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyList<ConversationSummary> GetRecentConversations()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        List<ConversationSummary> privateChats =
            (from chat in dbContext.PrivateChats.AsNoTracking()
             let latestMessage = dbContext.PrivateMessages
                 .Where(message => message.PrivateChatId == chat.Id)
                 .OrderByDescending(message => message.SentAt)
                 .ThenByDescending(message => message.Id)
                 .FirstOrDefault()
             select new ConversationSummary
             {
                 Kind = ConversationKind.PrivateChat,
                 Category = chat.Kind == PrivateChatKind.LocalUserAndAiAccount
                     ? ConversationCategory.MyPrivateChat
                     : ConversationCategory.FriendPrivateChat,
                 Id = chat.Id,
                 ContactId = chat.ContactId,
                 DisplayName = chat.Kind == PrivateChatKind.LocalUserAndAiAccount
                     ? chat.Contact!.AiAccount.Nickname
                     : chat.FirstAiAccount!.Nickname
                         + " 与 "
                         + chat.SecondAiAccount!.Nickname,
                 AvatarAiAccountId = chat.Kind == PrivateChatKind.LocalUserAndAiAccount
                     ? chat.Contact!.AiAccountId
                     : chat.FirstAiAccountId,
                 AvatarMediaId = chat.Kind == PrivateChatKind.LocalUserAndAiAccount
                     ? chat.Contact!.AiAccount.AvatarMediaId
                     : chat.FirstAiAccount!.AvatarMediaId,
                 MemberCount = chat.Kind == PrivateChatKind.LocalUserAndAiAccount
                     ? 1
                     : 2,
                 LatestSenderDisplayName = latestMessage == null
                     ? null
                     : latestMessage.SenderDisplayName,
                 LatestMessageContent = latestMessage == null
                     ? null
                     : latestMessage.Content,
                 LatestMessageAt = latestMessage == null
                     ? null
                     : latestMessage.SentAt,
                 CreatedAt = chat.CreatedAt
             }).ToList();

        List<ConversationSummary> groupChats =
            (from chat in dbContext.GroupChats.AsNoTracking()
             let latestMessage = dbContext.GroupMessages
                 .Where(message => message.GroupChatId == chat.Id)
                 .OrderByDescending(message => message.SentAt)
                 .ThenByDescending(message => message.Id)
                 .FirstOrDefault()
             select new ConversationSummary
             {
                 Kind = ConversationKind.GroupChat,
                 Category = chat.IncludesLocalUser
                     ? ConversationCategory.MyGroupChat
                     : ConversationCategory.FriendGroupChat,
                 Id = chat.Id,
                 ContactId = null,
                 DisplayName = chat.Name,
                 AvatarAiAccountId = null,
                 AvatarMediaId = null,
                 MemberCount = chat.Members.Count
                     + (chat.IncludesLocalUser ? 1 : 0),
                 LatestSenderDisplayName = latestMessage == null
                     ? null
                     : latestMessage.SenderDisplayName,
                 LatestMessageContent = latestMessage == null
                     ? null
                     : latestMessage.Content,
                 LatestMessageAt = latestMessage == null
                     ? null
                     : latestMessage.SentAt,
                 CreatedAt = chat.CreatedAt
             }).ToList();

        return privateChats
            .Concat(groupChats)
            .OrderByDescending(summary =>
                summary.LatestMessageAt ?? summary.CreatedAt)
            .ThenBy(summary => summary.Id)
            .ToList()
            .AsReadOnly();
    }
}

public enum ConversationKind
{
    PrivateChat,
    GroupChat
}

public sealed class ConversationSummary
{
    public ConversationKind Kind { get; init; }
    public ConversationCategory Category { get; init; }
    public Guid Id { get; init; }
    public Guid? ContactId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public Guid? AvatarAiAccountId { get; init; }
    public string? AvatarMediaId { get; init; }
    public int MemberCount { get; init; }
    public string? LatestSenderDisplayName { get; init; }
    public string? LatestMessageContent { get; init; }
    public DateTime? LatestMessageAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

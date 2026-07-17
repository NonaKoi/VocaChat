using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责好友私聊的创建、查询和消息持久化。
/// </summary>
public sealed class PrivateChatService
{
    private const int SqliteUniqueConstraintErrorCode = 2067;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    public PrivateChatService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public bool TryGetOrCreate(
        Guid contactId,
        out PrivateChat? privateChat,
        out bool created,
        out string errorMessage)
    {
        privateChat = null;
        created = false;
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.Contacts.Any(contact => contact.Id == contactId))
        {
            errorMessage = "好友不存在，不能创建私聊。";
            return false;
        }

        PrivateChat? existingChat = dbContext.PrivateChats
            .AsNoTracking()
            .Include(chat => chat.Contact)
                .ThenInclude(contact => contact.AiAccount)
                    .ThenInclude(aiAccount => aiAccount.Tags)
            .FirstOrDefault(chat => chat.ContactId == contactId);

        if (existingChat is not null)
        {
            privateChat = existingChat;
            errorMessage = string.Empty;
            return true;
        }

        PrivateChat newChat = new(contactId);
        dbContext.PrivateChats.Add(newChat);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            privateChat = FindByContactId(contactId);
            errorMessage = privateChat is null
                ? "私聊暂时无法创建，请重试。"
                : string.Empty;
            return privateChat is not null;
        }

        created = true;
        privateChat = FindById(newChat.Id);
        errorMessage = string.Empty;
        return true;
    }

    public PrivateChat? FindById(Guid privateChatId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.PrivateChats
            .AsNoTracking()
            .Include(chat => chat.Contact)
                .ThenInclude(contact => contact.AiAccount)
                    .ThenInclude(aiAccount => aiAccount.Tags)
            .Include(chat => chat.Contact)
                .ThenInclude(contact => contact.ContactGroup)
            .FirstOrDefault(chat => chat.Id == privateChatId);
    }

    public PrivateChat? FindByContactId(Guid contactId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.PrivateChats
            .AsNoTracking()
            .Include(chat => chat.Contact)
                .ThenInclude(contact => contact.AiAccount)
                    .ThenInclude(aiAccount => aiAccount.Tags)
            .Include(chat => chat.Contact)
                .ThenInclude(contact => contact.ContactGroup)
            .FirstOrDefault(chat => chat.ContactId == contactId);
    }

    public bool TrySaveUserMessage(
        PrivateChat privateChat,
        string content,
        out PrivateMessage? message,
        out string errorMessage)
    {
        return TrySaveMessage(
            privateChat,
            MessageSenderType.User,
            "我",
            null,
            content,
            out message,
            out errorMessage);
    }

    public bool TrySaveAiReply(
        PrivateChat privateChat,
        AiAccount aiAccount,
        string content,
        out PrivateMessage? message,
        out string errorMessage)
    {
        using VocaChatDbContext verificationContext =
            _dbContextFactory.CreateDbContext();
        bool isPrivateChatFriend = verificationContext.PrivateChats.Any(chat =>
            chat.Id == privateChat.Id
            && chat.Contact.AiAccountId == aiAccount.Id);

        if (!isPrivateChatFriend)
        {
            message = null;
            errorMessage = "只有当前私聊好友可以发送 AI 回复。";
            return false;
        }

        return TrySaveMessage(
            privateChat,
            MessageSenderType.AiAccount,
            aiAccount.Nickname,
            aiAccount.Id,
            content,
            out message,
            out errorMessage);
    }

    public IReadOnlyList<PrivateMessage> GetOrderedChatHistory(
        Guid privateChatId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.PrivateMessages
            .AsNoTracking()
            .Where(message => message.PrivateChatId == privateChatId)
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .ToList()
            .AsReadOnly();
    }

    private bool TrySaveMessage(
        PrivateChat privateChat,
        MessageSenderType senderType,
        string senderDisplayName,
        Guid? senderAiAccountId,
        string content,
        out PrivateMessage? message,
        out string errorMessage)
    {
        message = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "消息内容不能为空。";
            return false;
        }

        string trimmedContent = content.Trim();

        if (trimmedContent.Length > PrivateMessage.ContentMaxLength)
        {
            errorMessage = $"消息内容不能超过 {PrivateMessage.ContentMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.PrivateChats.Any(chat => chat.Id == privateChat.Id))
        {
            errorMessage = "私聊不存在，不能保存消息。";
            return false;
        }

        PrivateMessage newMessage = new(
            privateChat.Id,
            senderType,
            senderDisplayName,
            senderAiAccountId,
            trimmedContent);
        dbContext.PrivateMessages.Add(newMessage);
        dbContext.SaveChanges();
        message = newMessage;
        errorMessage = string.Empty;
        return true;
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                == SqliteUniqueConstraintErrorCode;
    }
}

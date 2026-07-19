using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责私信参与关系的创建、查询和消息持久化。
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

    /// <summary>
    /// 获取或创建本地用户与一个已有好友之间的私信。
    /// </summary>
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
            errorMessage = "好友不存在，不能创建私信。";
            return false;
        }

        PrivateChat? existingChat = BuildParticipantQuery(dbContext)
            .FirstOrDefault(chat =>
                chat.Kind == PrivateChatKind.LocalUserAndAiAccount
                && chat.ContactId == contactId);

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
                ? "私信暂时无法创建，请重试。"
                : string.Empty;
            return privateChat is not null;
        }

        created = true;
        privateChat = FindById(newChat.Id);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 获取或创建两个已有 AI 账号之间的唯一好友私信。
    /// </summary>
    public bool TryGetOrCreateAiPrivateChat(
        Guid firstAiAccountId,
        Guid secondAiAccountId,
        out PrivateChat? privateChat,
        out bool created,
        out string errorMessage)
    {
        privateChat = null;
        created = false;

        if (firstAiAccountId == secondAiAccountId)
        {
            errorMessage = "好友私信需要选择两个不同的好友。";
            return false;
        }

        (Guid normalizedFirstId, Guid normalizedSecondId) =
            NormalizeAiAccountPair(firstAiAccountId, secondAiAccountId);

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        int existingAccountCount = dbContext.AiAccounts.Count(account =>
            account.Id == normalizedFirstId
            || account.Id == normalizedSecondId);

        if (existingAccountCount != 2)
        {
            errorMessage = "选择的好友不存在，不能创建好友私信。";
            return false;
        }

        PrivateChat? existingChat = BuildParticipantQuery(dbContext)
            .FirstOrDefault(chat =>
                chat.Kind == PrivateChatKind.AiAccounts
                && chat.FirstAiAccountId == normalizedFirstId
                && chat.SecondAiAccountId == normalizedSecondId);

        if (existingChat is not null)
        {
            privateChat = existingChat;
            errorMessage = string.Empty;
            return true;
        }

        PrivateChat newChat = new(normalizedFirstId, normalizedSecondId);
        dbContext.PrivateChats.Add(newChat);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            privateChat = FindByAiAccountPair(
                normalizedFirstId,
                normalizedSecondId);
            errorMessage = privateChat is null
                ? "好友私信暂时无法创建，请重试。"
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
        return BuildParticipantQuery(dbContext)
            .FirstOrDefault(chat => chat.Id == privateChatId);
    }

    public PrivateChat? FindByContactId(Guid contactId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return BuildParticipantQuery(dbContext)
            .FirstOrDefault(chat =>
                chat.Kind == PrivateChatKind.LocalUserAndAiAccount
                && chat.ContactId == contactId);
    }

    public PrivateChat? FindByAiAccountPair(
        Guid firstAiAccountId,
        Guid secondAiAccountId)
    {
        (Guid normalizedFirstId, Guid normalizedSecondId) =
            NormalizeAiAccountPair(firstAiAccountId, secondAiAccountId);

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        return BuildParticipantQuery(dbContext)
            .FirstOrDefault(chat =>
                chat.Kind == PrivateChatKind.AiAccounts
                && chat.FirstAiAccountId == normalizedFirstId
                && chat.SecondAiAccountId == normalizedSecondId);
    }

    /// <summary>
    /// 返回私信中的 AI 参与者；本地用户不会作为 AI 账号返回。
    /// </summary>
    public IReadOnlyList<AiAccount> GetAiParticipants(PrivateChat privateChat)
    {
        if (privateChat.Kind == PrivateChatKind.LocalUserAndAiAccount)
        {
            return privateChat.Contact is null
                ? Array.Empty<AiAccount>()
                : new[] { privateChat.Contact.AiAccount };
        }

        return new[]
            {
                privateChat.FirstAiAccount,
                privateChat.SecondAiAccount
            }
            .OfType<AiAccount>()
            .ToList()
            .AsReadOnly();
    }

    public bool TrySaveUserMessage(
        PrivateChat privateChat,
        string content,
        out PrivateMessage? message,
        out string errorMessage)
    {
        if (privateChat.Kind != PrivateChatKind.LocalUserAndAiAccount)
        {
            message = null;
            errorMessage = "你不在这段好友私信中，不能发送用户消息。";
            return false;
        }

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
        bool saved = TrySaveAiReplies(
            privateChat,
            aiAccount,
            new[] { content },
            out IReadOnlyList<PrivateMessage> messages,
            out errorMessage);
        message = saved ? messages.Single() : null;
        return saved;
    }

    /// <summary>
    /// 将同一次 AI 回应的多条独立消息作为一个批次验证并保存，避免只留下半段回答。
    /// </summary>
    public bool TrySaveAiReplies(
        PrivateChat privateChat,
        AiAccount aiAccount,
        IReadOnlyList<string> contents,
        out IReadOnlyList<PrivateMessage> messages,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(contents);
        messages = Array.Empty<PrivateMessage>();

        if (contents.Count == 0)
        {
            errorMessage = "AI 回复至少需要包含一条消息。";
            return false;
        }

        List<string> normalizedContents = new(contents.Count);
        foreach (string content in contents)
        {
            if (!TryNormalizeMessageContent(
                    content,
                    out string normalizedContent,
                    out errorMessage))
            {
                return false;
            }

            normalizedContents.Add(normalizedContent);
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        bool isPrivateChatParticipant = dbContext.PrivateChats.Any(chat =>
            chat.Id == privateChat.Id
            && ((chat.Kind == PrivateChatKind.LocalUserAndAiAccount
                    && chat.Contact != null
                    && chat.Contact.AiAccountId == aiAccount.Id)
                || (chat.Kind == PrivateChatKind.AiAccounts
                    && (chat.FirstAiAccountId == aiAccount.Id
                        || chat.SecondAiAccountId == aiAccount.Id))));

        if (!isPrivateChatParticipant)
        {
            errorMessage = "只有当前私信的 AI 参与者可以发送消息。";
            return false;
        }

        DateTime firstSentAt = DateTime.Now;
        List<PrivateMessage> newMessages = normalizedContents
            .Select((content, index) => new PrivateMessage(
                privateChat.Id,
                MessageSenderType.AiAccount,
                aiAccount.Nickname,
                aiAccount.Id,
                content,
                firstSentAt.AddTicks(index)))
            .ToList();
        dbContext.PrivateMessages.AddRange(newMessages);
        dbContext.SaveChanges();
        messages = newMessages.AsReadOnly();
        errorMessage = string.Empty;
        return true;
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

        if (!TryNormalizeMessageContent(
                content,
                out string trimmedContent,
                out errorMessage))
        {
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.PrivateChats.Any(chat => chat.Id == privateChat.Id))
        {
            errorMessage = "私信不存在，不能保存消息。";
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

    private static bool TryNormalizeMessageContent(
        string content,
        out string normalizedContent,
        out string errorMessage)
    {
        normalizedContent = string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "消息内容不能为空。";
            return false;
        }

        normalizedContent = content.Trim();

        if (normalizedContent.Length > PrivateMessage.ContentMaxLength)
        {
            errorMessage = $"消息内容不能超过 {PrivateMessage.ContentMaxLength} 个字符。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static IQueryable<PrivateChat> BuildParticipantQuery(
        VocaChatDbContext dbContext)
    {
        return dbContext.PrivateChats
            .AsNoTracking()
            .Include(chat => chat.Contact)
                .ThenInclude(contact => contact!.AiAccount)
                    .ThenInclude(aiAccount => aiAccount.Tags)
            .Include(chat => chat.Contact)
                .ThenInclude(contact => contact!.ContactGroup)
            .Include(chat => chat.FirstAiAccount)
                .ThenInclude(aiAccount => aiAccount!.Tags)
            .Include(chat => chat.SecondAiAccount)
                .ThenInclude(aiAccount => aiAccount!.Tags);
    }

    private static (Guid FirstId, Guid SecondId) NormalizeAiAccountPair(
        Guid firstAiAccountId,
        Guid secondAiAccountId)
    {
        return firstAiAccountId.CompareTo(secondAiAccountId) <= 0
            ? (firstAiAccountId, secondAiAccountId)
            : (secondAiAccountId, firstAiAccountId);
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                == SqliteUniqueConstraintErrorCode;
    }
}

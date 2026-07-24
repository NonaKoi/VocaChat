using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责好友关系、好友分组和分组调整。
/// </summary>
public sealed class ContactService
{
    private const int SqliteUniqueConstraintErrorCode = 2067;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    public ContactService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public IReadOnlyList<Contact> GetAllContacts()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Contacts
            .AsNoTracking()
            .Include(contact => contact.AiAccount)
                .ThenInclude(aiAccount => aiAccount.Tags)
            .Include(contact => contact.AiAccount)
                .ThenInclude(aiAccount => aiAccount.CharacterWorld)
            .Include(contact => contact.ContactGroup)
            .OrderBy(contact => contact.ContactGroup.SortOrder)
            .ThenBy(contact => contact.CreatedAt)
            .ThenBy(contact => contact.Id)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ContactGroup> GetAllGroups()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.ContactGroups
            .AsNoTracking()
            .OrderBy(group => group.SortOrder)
            .ThenBy(group => group.CreatedAt)
            .ThenBy(group => group.Id)
            .ToList()
            .AsReadOnly();
    }

    public Contact? FindById(Guid contactId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Contacts
            .AsNoTracking()
            .Include(contact => contact.AiAccount)
                .ThenInclude(aiAccount => aiAccount.Tags)
            .Include(contact => contact.AiAccount)
                .ThenInclude(aiAccount => aiAccount.CharacterWorld)
            .Include(contact => contact.ContactGroup)
            .FirstOrDefault(contact => contact.Id == contactId);
    }

    public Contact? FindByAiAccountId(Guid aiAccountId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Contacts
            .AsNoTracking()
            .Include(contact => contact.AiAccount)
                .ThenInclude(aiAccount => aiAccount.Tags)
            .Include(contact => contact.AiAccount)
                .ThenInclude(aiAccount => aiAccount.CharacterWorld)
            .Include(contact => contact.ContactGroup)
            .FirstOrDefault(contact => contact.AiAccountId == aiAccountId);
    }

    public bool TryCreateGroup(
        string name,
        out ContactGroup? group,
        out string errorMessage)
    {
        group = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "好友分组名称不能为空。";
            return false;
        }

        string trimmedName = name.Trim();

        if (trimmedName.Length > ContactGroup.NameMaxLength)
        {
            errorMessage = $"好友分组名称不能超过 {ContactGroup.NameMaxLength} 个字符。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (dbContext.ContactGroups.Any(storedGroup =>
                storedGroup.Name == trimmedName))
        {
            errorMessage = "好友分组名称已存在。";
            return false;
        }

        int nextSortOrder = dbContext.ContactGroups
            .Select(storedGroup => (int?)storedGroup.SortOrder)
            .Max() is int maximumSortOrder
                ? maximumSortOrder + 1
                : 0;
        ContactGroup newGroup = new(trimmedName, nextSortOrder);
        dbContext.ContactGroups.Add(newGroup);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            errorMessage = "好友分组名称已存在。";
            return false;
        }

        group = newGroup;
        errorMessage = string.Empty;
        return true;
    }

    public bool TryMoveContact(
        Guid contactId,
        Guid contactGroupId,
        out Contact? contact,
        out string errorMessage)
    {
        contact = null;
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        Contact? storedContact = dbContext.Contacts
            .FirstOrDefault(candidate => candidate.Id == contactId);

        if (storedContact is null)
        {
            errorMessage = "好友不存在。";
            return false;
        }

        if (!dbContext.ContactGroups.Any(group => group.Id == contactGroupId))
        {
            errorMessage = "好友分组不存在。";
            return false;
        }

        storedContact.MoveToGroup(contactGroupId);
        dbContext.SaveChanges();
        errorMessage = string.Empty;

        contact = dbContext.Contacts
            .AsNoTracking()
            .Include(candidate => candidate.AiAccount)
                .ThenInclude(aiAccount => aiAccount.Tags)
            .Include(candidate => candidate.AiAccount)
                .ThenInclude(aiAccount => aiAccount.CharacterWorld)
            .Include(candidate => candidate.ContactGroup)
            .Single(candidate => candidate.Id == contactId);
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

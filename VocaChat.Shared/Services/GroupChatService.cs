using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责群聊和群成员关系的验证、数据库保存与查询。
/// </summary>
public class GroupChatService
{
    private const int SqlitePrimaryKeyConstraintErrorCode = 1555;
    private const int SqliteUniqueConstraintErrorCode = 2067;

    private readonly VocaChatDbContextFactory _dbContextFactory;

    /// <summary>
    /// 创建群聊 Service；每个业务操作使用一个短生命周期 DbContext。
    /// </summary>
    public GroupChatService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 验证群聊名称；验证失败时返回可显示的错误信息。
    /// </summary>
    public string? ValidateGroupChatName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "群聊名称不能为空。";
        }

        if (name.Trim().Length > GroupChat.NameMaxLength)
        {
            return $"群聊名称不能超过 {GroupChat.NameMaxLength} 个字符。";
        }

        return null;
    }

    /// <summary>
    /// 使用数据库中已经存在的 AI 账号创建群聊和成员关系。
    /// </summary>
    public bool TryCreateGroupChat(
        string name,
        IEnumerable<Guid> selectedAiAccountIds,
        out GroupChat? groupChat,
        out string errorMessage)
    {
        return TryCreateGroupChat(
            name,
            selectedAiAccountIds,
            includesLocalUser: true,
            out groupChat,
            out errorMessage);
    }

    /// <summary>
    /// 使用已有 AI 账号创建群聊，并明确本地用户是否属于该群聊。
    /// </summary>
    public bool TryCreateGroupChat(
        string name,
        IEnumerable<Guid> selectedAiAccountIds,
        bool includesLocalUser,
        out GroupChat? groupChat,
        out string errorMessage)
    {
        groupChat = null;

        string? validationError = ValidateGroupChatName(name);

        if (validationError is not null)
        {
            errorMessage = validationError;
            return false;
        }

        if (selectedAiAccountIds is null)
        {
            errorMessage = "群聊至少需要一个 AI 成员。";
            return false;
        }

        List<Guid> distinctAiAccountIds = selectedAiAccountIds
            .Distinct()
            .ToList();

        if (distinctAiAccountIds.Count == 0)
        {
            errorMessage = "群聊至少需要一个 AI 成员。";
            return false;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        List<AiAccount> existingAiAccounts = dbContext.AiAccounts
            .Where(aiAccount => distinctAiAccountIds.Contains(aiAccount.Id))
            .ToList();

        if (existingAiAccounts.Count != distinctAiAccountIds.Count)
        {
            errorMessage = "选择的 AI 账号不存在，不能加入群聊。";
            return false;
        }

        GroupChat newGroupChat = new(name.Trim(), includesLocalUser);

        foreach (Guid aiAccountId in distinctAiAccountIds)
        {
            AiAccount member = existingAiAccounts.Single(
                aiAccount => aiAccount.Id == aiAccountId);
            newGroupChat.AddMember(member);
        }

        dbContext.GroupChats.Add(newGroupChat);
        dbContext.SaveChanges();

        groupChat = newGroupChat;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 按完整成员集合复用已有好友群聊；没有匹配群聊时创建一个不含本地用户的新群聊。
    /// </summary>
    public bool TryGetOrCreateFriendGroupChat(
        string name,
        IEnumerable<Guid> memberAiAccountIds,
        out GroupChat? groupChat,
        out bool groupChatCreated,
        out string errorMessage)
    {
        List<Guid> distinctMemberIds = memberAiAccountIds is null
            ? new List<Guid>()
            : memberAiAccountIds.Distinct().ToList();

        if (distinctMemberIds.Count == 0)
        {
            groupChat = null;
            groupChatCreated = false;
            errorMessage = "好友群聊至少需要一个成员。";
            return false;
        }

        HashSet<Guid> expectedMemberIds = distinctMemberIds.ToHashSet();
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        GroupChat? existingGroupChat = dbContext.GroupChats
            .AsNoTracking()
            .Include(storedGroupChat => storedGroupChat.Members)
            .Where(storedGroupChat => !storedGroupChat.IncludesLocalUser)
            .AsEnumerable()
            .FirstOrDefault(storedGroupChat =>
                storedGroupChat.Members.Count == expectedMemberIds.Count
                && storedGroupChat.Members.All(member =>
                    expectedMemberIds.Contains(member.Id)));

        if (existingGroupChat is not null)
        {
            groupChat = existingGroupChat;
            groupChatCreated = false;
            errorMessage = string.Empty;
            return true;
        }

        bool created = TryCreateGroupChat(
            name,
            distinctMemberIds,
            includesLocalUser: false,
            out groupChat,
            out errorMessage);
        groupChatCreated = created;
        return created;
    }

    /// <summary>
    /// 将数据库中已有的 AI 账号加入已保存群聊，并阻止重复加入。
    /// </summary>
    public bool TryAddMember(
        GroupChat groupChat,
        Guid aiAccountId,
        out string errorMessage)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        GroupChat? storedGroupChat = dbContext.GroupChats
            .Include(storedGroupChat => storedGroupChat.Members)
            .SingleOrDefault(storedGroupChat => storedGroupChat.Id == groupChat.Id);

        if (storedGroupChat is null)
        {
            errorMessage = "群聊不存在，不能添加成员。";
            return false;
        }

        AiAccount? aiAccount = dbContext.AiAccounts
            .SingleOrDefault(aiAccount => aiAccount.Id == aiAccountId);

        if (aiAccount is null)
        {
            errorMessage = "AI 账号不存在，不能加入群聊。";
            return false;
        }

        if (storedGroupChat.Members.Any(member => member.Id == aiAccountId))
        {
            errorMessage = "该 AI 账号已经是群成员。";
            return false;
        }

        storedGroupChat.AddMember(aiAccount);

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
            when (IsDuplicateMemberConstraintViolation(exception))
        {
            errorMessage = "该 AI 账号已经是群成员。";
            return false;
        }

        if (!groupChat.Members.Any(member => member.Id == aiAccountId))
        {
            groupChat.AddMember(aiAccount);
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 根据数据库成员关系判断指定 AI 账号是否属于当前群聊。
    /// </summary>
    public bool IsMember(GroupChat groupChat, Guid aiAccountId)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.GroupChats.Any(storedGroupChat =>
            storedGroupChat.Id == groupChat.Id
            && storedGroupChat.Members.Any(member => member.Id == aiAccountId));
    }

    /// <summary>
    /// 从数据库返回当前群聊成员的只读列表。
    /// </summary>
    public IReadOnlyList<AiAccount> GetMembers(GroupChat groupChat)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        List<AiAccount> members = dbContext.GroupChats
            .AsNoTracking()
            .Where(storedGroupChat => storedGroupChat.Id == groupChat.Id)
            .SelectMany(storedGroupChat => storedGroupChat.Members)
            .OrderBy(member => member.CreatedAt)
            .ThenBy(member => member.Id)
            .ToList();

        return members.AsReadOnly();
    }

    /// <summary>
    /// 按 Id 查询群聊并加载成员；未找到时返回 null。
    /// </summary>
    public GroupChat? FindById(Guid id)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.GroupChats
            .AsNoTracking()
            .Include(groupChat => groupChat.Members)
            .SingleOrDefault(groupChat => groupChat.Id == id);
    }

    /// <summary>
    /// 按创建时间返回全部群聊及其成员的只读列表。
    /// </summary>
    public IReadOnlyList<GroupChat> GetAllGroupChats()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        List<GroupChat> groupChats = dbContext.GroupChats
            .AsNoTracking()
            .Include(groupChat => groupChat.Members)
            .OrderBy(groupChat => groupChat.CreatedAt)
            .ThenBy(groupChat => groupChat.Id)
            .ToList();

        return groupChats.AsReadOnly();
    }

    /// <summary>
    /// 只将成员中间表的主键或唯一约束冲突转换为重复成员错误。
    /// </summary>
    private static bool IsDuplicateMemberConstraintViolation(
        DbUpdateException exception)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        return sqliteException.SqliteExtendedErrorCode
            is SqlitePrimaryKeyConstraintErrorCode
            or SqliteUniqueConstraintErrorCode;
    }
}

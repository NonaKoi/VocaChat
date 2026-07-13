using System;
using System.Collections.Generic;
using System.Linq;
using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Services;

/// <summary>
/// 负责群聊的创建、内存保存、查询和成员管理。
/// </summary>
public class GroupChatService
{
    private readonly AiAccountService _aiAccountService;
    private readonly List<GroupChat> _groupChats = new();

    /// <summary>
    /// 创建群聊 Service，并使用账号 Service 验证群成员是否真实存在。
    /// </summary>
    public GroupChatService(AiAccountService aiAccountService)
    {
        _aiAccountService = aiAccountService;
    }

    /// <summary>
    /// 验证群聊名称；验证失败时返回可显示的错误信息。
    /// </summary>
    public string? ValidateGroupChatName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? "群聊名称不能为空。"
            : null;
    }

    /// <summary>
    /// 使用用户已经创建的 AI 账号 Id 创建并保存群聊。
    /// </summary>
    public bool TryCreateGroupChat(
        string name,
        IEnumerable<Guid> selectedAiAccountIds,
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

        List<Guid> distinctAiAccountIds = new();

        foreach (Guid aiAccountId in selectedAiAccountIds)
        {
            if (!distinctAiAccountIds.Contains(aiAccountId))
            {
                distinctAiAccountIds.Add(aiAccountId);
            }
        }

        if (distinctAiAccountIds.Count == 0)
        {
            errorMessage = "群聊至少需要一个 AI 成员。";
            return false;
        }

        List<AiAccount> members = new();

        foreach (Guid aiAccountId in distinctAiAccountIds)
        {
            AiAccount? aiAccount = _aiAccountService.FindById(aiAccountId);

            if (aiAccount is null)
            {
                errorMessage = "选择的 AI 账号不存在，不能加入群聊。";
                return false;
            }

            members.Add(aiAccount);
        }

        GroupChat newGroupChat = new(name.Trim());

        foreach (AiAccount member in members)
        {
            newGroupChat.AddMember(member);
        }

        _groupChats.Add(newGroupChat);
        groupChat = newGroupChat;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 将一个已经创建的 AI 账号加入已保存的群聊，并阻止重复加入。
    /// </summary>
    public bool TryAddMember(
        GroupChat groupChat,
        Guid aiAccountId,
        out string errorMessage)
    {
        if (FindById(groupChat.Id) is null)
        {
            errorMessage = "群聊不存在，不能添加成员。";
            return false;
        }

        AiAccount? aiAccount = _aiAccountService.FindById(aiAccountId);

        if (aiAccount is null)
        {
            errorMessage = "AI 账号不存在，不能加入群聊。";
            return false;
        }

        if (IsMember(groupChat, aiAccountId))
        {
            errorMessage = "该 AI 账号已经是群成员。";
            return false;
        }

        groupChat.AddMember(aiAccount);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 判断指定 AI 账号是否属于当前群聊。
    /// </summary>
    public bool IsMember(GroupChat groupChat, Guid aiAccountId)
    {
        return groupChat.Members.Any(member => member.Id == aiAccountId);
    }

    /// <summary>
    /// 返回当前群聊成员的只读副本。
    /// </summary>
    public IReadOnlyList<AiAccount> GetMembers(GroupChat groupChat)
    {
        List<AiAccount> memberSnapshot = new(groupChat.Members);
        return memberSnapshot.AsReadOnly();
    }

    /// <summary>
    /// 按 Id 查找已保存的群聊；未找到时返回 null。
    /// </summary>
    public GroupChat? FindById(Guid id)
    {
        return _groupChats.FirstOrDefault(groupChat => groupChat.Id == id);
    }

    /// <summary>
    /// 返回当前全部群聊的只读副本。
    /// </summary>
    public IReadOnlyList<GroupChat> GetAllGroupChats()
    {
        List<GroupChat> groupChatSnapshot = new(_groupChats);
        return groupChatSnapshot.AsReadOnly();
    }
}

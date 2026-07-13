using System;
using System.Collections.Generic;
using System.Linq;
using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Services;

/// <summary>
/// 负责 AI 账号的内存保存、昵称验证、创建和查找。
/// </summary>
public class AiAccountService
{
    private readonly List<AiAccount> _aiAccounts = new();

    /// <summary>
    /// 验证昵称是否可以用于新账号；验证失败时返回可显示的错误信息。
    /// </summary>
    public string? ValidateNickname(string nickname)
    {
        string trimmedNickname = nickname.Trim();

        if (string.IsNullOrWhiteSpace(trimmedNickname))
        {
            return "昵称不能为空。";
        }

        if (FindByNickname(trimmedNickname) is not null)
        {
            return "昵称已存在。";
        }

        return null;
    }

    /// <summary>
    /// 验证并创建 AI 账号；成功时保存到内存集合，失败时返回明确错误信息。
    /// </summary>
    public bool TryCreateAiAccount(
        string nickname,
        string identityDescription,
        string personality,
        string speakingStyle,
        out AiAccount? aiAccount,
        out string errorMessage)
    {
        aiAccount = null;

        string? validationError = ValidateNickname(nickname);

        if (validationError is not null)
        {
            errorMessage = validationError;
            return false;
        }

        aiAccount = new AiAccount(
            nickname.Trim(),
            identityDescription.Trim(),
            personality.Trim(),
            speakingStyle.Trim());

        _aiAccounts.Add(aiAccount);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 按 Id 查找 AI 账号；未找到时返回 null。
    /// </summary>
    public AiAccount? FindById(Guid id)
    {
        return _aiAccounts.FirstOrDefault(account => account.Id == id);
    }

    /// <summary>
    /// 按昵称查找 AI 账号；比较时忽略大小写。
    /// </summary>
    public AiAccount? FindByNickname(string nickname)
    {
        string trimmedNickname = nickname.Trim();

        return _aiAccounts.FirstOrDefault(account =>
            account.Nickname.Equals(trimmedNickname, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 返回当前全部 AI 账号的只读副本，避免外部修改 Service 内部集合。
    /// </summary>
    public IReadOnlyList<AiAccount> GetAllAccounts()
    {
        List<AiAccount> accountSnapshot = new(_aiAccounts);
        return accountSnapshot.AsReadOnly();
    }
}

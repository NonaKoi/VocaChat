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

    public IReadOnlyList<AiAccount> Accounts => _aiAccounts;

    /// <summary>
    /// 验证昵称是否可以用于新账号；验证失败时返回可显示的错误信息。
    /// </summary>
    public string? ValidateNickname(string nickname)
    {
        string trimmedNickname = nickname.Trim();

        if (string.IsNullOrWhiteSpace(trimmedNickname))
        {
            return "昵称不能为空，请重新输入。";
        }

        if (FindByNickname(trimmedNickname) is not null)
        {
            return "昵称已存在，请换一个昵称。";
        }

        return null;
    }

    /// <summary>
    /// 创建一个 AI 账号，并保存到当前进程的内存账号集合。
    /// </summary>
    public AiAccount CreateAiAccount(
        string nickname,
        string identityDescription,
        string personality,
        string speakingStyle)
    {
        string? validationError = ValidateNickname(nickname);

        if (validationError is not null)
        {
            throw new ArgumentException(validationError, nameof(nickname));
        }

        AiAccount aiAccount = new(
            nickname.Trim(),
            identityDescription.Trim(),
            personality.Trim(),
            speakingStyle.Trim());

        _aiAccounts.Add(aiAccount);
        return aiAccount;
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
}

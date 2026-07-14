using System;

namespace VocaChat.ConsoleApp.Models;

/// <summary>
/// 表示由当前本地用户创建的一个长期存在的 AI 账号。
/// </summary>
public class AiAccount
{
    internal const int NicknameMaxLength = 50;
    internal const int IdentityDescriptionMaxLength = 500;
    internal const int PersonalityMaxLength = 200;
    internal const int SpeakingStyleMaxLength = 200;

    public Guid Id { get; private set; }
    public string Nickname { get; private set; }
    public string IdentityDescription { get; private set; }
    public string Personality { get; private set; }
    public string SpeakingStyle { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// 供 EF Core 从数据库还原实体使用。
    /// </summary>
    private AiAccount()
    {
        Nickname = string.Empty;
        IdentityDescription = string.Empty;
        Personality = string.Empty;
        SpeakingStyle = string.Empty;
    }

    /// <summary>
    /// 创建 AI 账号，并在创建时生成唯一 Id 和创建时间。
    /// </summary>
    public AiAccount(
        string nickname,
        string identityDescription,
        string personality,
        string speakingStyle)
    {
        Id = Guid.NewGuid();
        Nickname = nickname;
        IdentityDescription = identityDescription;
        Personality = personality;
        SpeakingStyle = speakingStyle;
        CreatedAt = DateTime.Now;
    }
}

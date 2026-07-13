using System;

namespace VocaChat.ConsoleApp.Models;

/// <summary>
/// 表示由当前本地用户创建的一个长期存在的 AI 账号。
/// </summary>
public class AiAccount
{
    public Guid Id { get; }
    public string Nickname { get; }
    public string IdentityDescription { get; }
    public string Personality { get; }
    public string SpeakingStyle { get; }
    public DateTime CreatedAt { get; }

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

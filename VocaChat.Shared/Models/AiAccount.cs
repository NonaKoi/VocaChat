using System;
using System.Collections.Generic;

namespace VocaChat.Models;

/// <summary>
/// 表示由当前本地用户创建的一个长期存在的 AI 账号。
/// </summary>
public class AiAccount
{
    internal const int NicknameMaxLength = 50;
    internal const int VcNumberMaxLength = 32;
    internal const int IdentityDescriptionMaxLength = 500;
    internal const int PersonalityMaxLength = 200;
    internal const int SpeakingStyleMaxLength = 200;
    internal const int SignatureMaxLength = 200;
    internal const int LocationMaxLength = 100;
    internal const int OccupationMaxLength = 100;
    internal const int HometownMaxLength = 100;
    internal const int MediaIdMaxLength = 80;

    private readonly List<AiAccountTag> _tags = new();

    public Guid Id { get; private set; }
    public string VcNumber { get; private set; }
    public string Nickname { get; private set; }
    public string IdentityDescription { get; private set; }
    public string Personality { get; private set; }
    public string SpeakingStyle { get; private set; }
    public string Signature { get; private set; }
    public DateOnly? Birthday { get; private set; }
    public AiAccountGender Gender { get; private set; }
    public string Location { get; private set; }
    public string Occupation { get; private set; }
    public string Hometown { get; private set; }
    public OnlineStatus OnlineStatus { get; private set; }
    public string? AvatarMediaId { get; private set; }
    public string? ProfileCoverMediaId { get; private set; }
    public IReadOnlyList<AiAccountTag> Tags => _tags.AsReadOnly();
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// 供 EF Core 从数据库还原实体使用。
    /// </summary>
    private AiAccount()
    {
        VcNumber = string.Empty;
        Nickname = string.Empty;
        IdentityDescription = string.Empty;
        Personality = string.Empty;
        SpeakingStyle = string.Empty;
        Signature = string.Empty;
        Location = string.Empty;
        Occupation = string.Empty;
        Hometown = string.Empty;
    }

    /// <summary>
    /// 创建 AI 账号，并在创建时生成唯一 Id 和创建时间。
    /// </summary>
    public AiAccount(
        string vcNumber,
        string nickname,
        string identityDescription,
        string personality,
        string speakingStyle)
    {
        Id = Guid.NewGuid();
        VcNumber = vcNumber;
        Nickname = nickname;
        IdentityDescription = identityDescription;
        Personality = personality;
        SpeakingStyle = speakingStyle;
        Signature = string.Empty;
        Location = string.Empty;
        Occupation = string.Empty;
        Hometown = string.Empty;
        Gender = AiAccountGender.Unspecified;
        OnlineStatus = OnlineStatus.Offline;
        CreatedAt = DateTime.Now;
    }

    /// <summary>
    /// 创建带有完整好友档案的 AI 账号。
    /// </summary>
    internal AiAccount(
        string vcNumber,
        string nickname,
        string identityDescription,
        string personality,
        string speakingStyle,
        string signature,
        DateOnly? birthday,
        AiAccountGender gender,
        string location,
        string occupation,
        string hometown,
        OnlineStatus onlineStatus)
        : this(
            vcNumber,
            nickname,
            identityDescription,
            personality,
            speakingStyle)
    {
        Signature = signature;
        Birthday = birthday;
        Gender = gender;
        Location = location;
        Occupation = occupation;
        Hometown = hometown;
        OnlineStatus = onlineStatus;
    }

    /// <summary>
    /// 修改面向用户展示的 VC号；内部 Guid 主键不会随之改变。
    /// </summary>
    internal void ChangeVcNumber(string vcNumber)
    {
        VcNumber = vcNumber;
    }

    /// <summary>
    /// 更新头像对应的本地媒体标识；实体不保存文件系统绝对路径。
    /// </summary>
    internal void ChangeAvatarMediaId(string mediaId)
    {
        AvatarMediaId = mediaId;
    }

    /// <summary>
    /// 更新主页封面对应的本地媒体标识；实体不保存文件系统绝对路径。
    /// </summary>
    internal void ChangeProfileCoverMediaId(string mediaId)
    {
        ProfileCoverMediaId = mediaId;
    }

    /// <summary>
    /// 添加一个已经由 Service 清理并验证过的结构化标签。
    /// </summary>
    internal void AddTag(AiAccountTagType type, string value)
    {
        _tags.Add(new AiAccountTag(Id, type, value));
    }

    /// <summary>
    /// 根据指定日期计算年龄；生日未填写时返回 null。
    /// </summary>
    public int? CalculateAge(DateOnly referenceDate)
    {
        if (Birthday is null)
        {
            return null;
        }

        DateOnly birthday = Birthday.Value;
        int age = referenceDate.Year - birthday.Year;

        if (referenceDate < birthday.AddYears(age))
        {
            age--;
        }

        return Math.Max(age, 0);
    }

    /// <summary>
    /// 根据生日计算星座；生日未填写时返回 null。
    /// </summary>
    public string? GetZodiacSign()
    {
        if (Birthday is null)
        {
            return null;
        }

        int month = Birthday.Value.Month;
        int day = Birthday.Value.Day;

        return (month, day) switch
        {
            (1, >= 20) or (2, <= 18) => "水瓶座",
            (2, >= 19) or (3, <= 20) => "双鱼座",
            (3, >= 21) or (4, <= 19) => "白羊座",
            (4, >= 20) or (5, <= 20) => "金牛座",
            (5, >= 21) or (6, <= 21) => "双子座",
            (6, >= 22) or (7, <= 22) => "巨蟹座",
            (7, >= 23) or (8, <= 22) => "狮子座",
            (8, >= 23) or (9, <= 22) => "处女座",
            (9, >= 23) or (10, <= 23) => "天秤座",
            (10, >= 24) or (11, <= 22) => "天蝎座",
            (11, >= 23) or (12, <= 21) => "射手座",
            _ => "摩羯座"
        };
    }
}

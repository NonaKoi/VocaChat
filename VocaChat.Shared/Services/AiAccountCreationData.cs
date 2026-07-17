using System;
using System.Collections.Generic;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 汇总创建一个完整 AI 账号档案所需的数据，避免业务方法出现过长参数列表。
/// </summary>
public sealed class AiAccountCreationData
{
    public string Nickname { get; init; } = string.Empty;
    public string? VcNumber { get; init; }
    public string IdentityDescription { get; init; } = string.Empty;
    public string Personality { get; init; } = string.Empty;
    public string SpeakingStyle { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public DateOnly? Birthday { get; init; }
    public AiAccountGender Gender { get; init; } = AiAccountGender.Unspecified;
    public string Location { get; init; } = string.Empty;
    public string Occupation { get; init; } = string.Empty;
    public string Hometown { get; init; } = string.Empty;
    public OnlineStatus OnlineStatus { get; init; } = OnlineStatus.Offline;
    public IReadOnlyCollection<string> InterestTags { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> PersonalityTags { get; init; } = Array.Empty<string>();
}

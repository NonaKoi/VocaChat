using VocaChat.Models;
using VocaChat.WebApi.Dtos.AiAccounts;

namespace VocaChat.WebApi.Mapping;

/// <summary>
/// 将内部 AI 账号实体映射为稳定的 HTTP 好友资料响应。
/// </summary>
public static class AiAccountResponseMapper
{
    public static AiAccountResponse ToResponse(AiAccount aiAccount)
    {
        CharacterWorld characterWorld = aiAccount.CharacterWorld
            ?? throw new InvalidOperationException(
                "AI 账号响应缺少角色世界数据。");

        return new AiAccountResponse
        {
            Id = aiAccount.Id,
            VcNumber = aiAccount.VcNumber,
            Nickname = aiAccount.Nickname,
            IdentityDescription = aiAccount.IdentityDescription,
            Personality = aiAccount.Personality,
            SpeakingStyle = aiAccount.SpeakingStyle,
            Signature = aiAccount.Signature,
            Birthday = aiAccount.Birthday,
            Age = aiAccount.CalculateAge(DateOnly.FromDateTime(DateTime.Today)),
            ZodiacSign = aiAccount.GetZodiacSign(),
            Gender = aiAccount.Gender.ToString(),
            Location = aiAccount.Location,
            Occupation = aiAccount.Occupation,
            Hometown = aiAccount.Hometown,
            OnlineStatus = aiAccount.OnlineStatus.ToString(),
            AvatarUrl = AiAccountMediaUrls.GetAvatarUrl(aiAccount),
            CoverUrl = AiAccountMediaUrls.GetCoverUrl(aiAccount),
            CharacterWorldId = aiAccount.CharacterWorldId,
            CharacterWorld =
                CharacterWorldResponseMapper.ToResponse(characterWorld),
            InterestTags = aiAccount.Tags
                .Where(tag => tag.Type == AiAccountTagType.Interest)
                .OrderBy(tag => tag.Value)
                .Select(tag => tag.Value)
                .ToList(),
            PersonalityTags = aiAccount.Tags
                .Where(tag => tag.Type == AiAccountTagType.Personality)
                .OrderBy(tag => tag.Value)
                .Select(tag => tag.Value)
                .ToList(),
            CreatedAt = aiAccount.CreatedAt
        };
    }
}

using VocaChat.Models;

namespace VocaChat.WebApi.Mapping;

/// <summary>
/// 将内部媒体标识转换为客户端可访问且可随替换更新缓存的相对 URL。
/// </summary>
public static class AiAccountMediaUrls
{
    public static string? GetAvatarUrl(AiAccount aiAccount)
    {
        return GetAvatarUrl(
            aiAccount.Id,
            aiAccount.AvatarMediaId);
    }

    public static string? GetAvatarUrl(Guid aiAccountId, string? mediaId)
    {
        return BuildUrl(aiAccountId, "avatar", mediaId);
    }

    public static string? GetCoverUrl(AiAccount aiAccount)
    {
        return BuildUrl(
            aiAccount.Id,
            "cover",
            aiAccount.ProfileCoverMediaId);
    }

    private static string? BuildUrl(
        Guid aiAccountId,
        string mediaName,
        string? mediaId)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return null;
        }

        return $"/api/ai-accounts/{aiAccountId}/{mediaName}"
            + $"?v={Uri.EscapeDataString(mediaId)}";
    }
}

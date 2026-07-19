using VocaChat.Models;
using VocaChat.WebApi.Dtos.GroupChats;

namespace VocaChat.WebApi.Mapping;

/// <summary>
/// 将群聊实体映射为不暴露 EF Core 导航结构的 HTTP 响应。
/// </summary>
internal static class GroupChatResponseMapper
{
    public static GroupChatResponse ToResponse(GroupChat groupChat)
    {
        return new GroupChatResponse
        {
            Id = groupChat.Id,
            Name = groupChat.Name,
            IncludesLocalUser = groupChat.IncludesLocalUser,
            CreatedAt = groupChat.CreatedAt,
            Members = groupChat.Members
                .Select(account => new GroupChatMemberResponse
                {
                    Id = account.Id,
                    Nickname = account.Nickname,
                    AvatarUrl = AiAccountMediaUrls.GetAvatarUrl(account)
                })
                .ToList()
        };
    }
}

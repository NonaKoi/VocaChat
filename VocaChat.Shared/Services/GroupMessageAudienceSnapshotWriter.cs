using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 在群消息落库事务中写入当时能够看见该消息的 AI 群成员快照。
/// </summary>
internal static class GroupMessageAudienceSnapshotWriter
{
    /// <summary>
    /// 将当前群聊中的合法 AI 接收者加入 DbContext。
    /// 调用方负责把消息和这些快照放在同一次 SaveChanges 中保存。
    /// </summary>
    public static void AddSnapshot(
        VocaChatDbContext dbContext,
        GroupMessage message)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(message);

        List<Guid> audienceAccountIds = dbContext.GroupChats
            .AsNoTracking()
            .Where(groupChat => groupChat.Id == message.GroupChatId)
            .SelectMany(groupChat => groupChat.Members)
            .Select(member => member.Id)
            .Distinct()
            .ToList();

        if (audienceAccountIds.Count == 0)
        {
            return;
        }

        dbContext.GroupMessageAudience.AddRange(
            audienceAccountIds.Select(aiAccountId =>
                new GroupMessageAudience(
                    message.Id,
                    aiAccountId,
                    message.SentAt)));
    }
}

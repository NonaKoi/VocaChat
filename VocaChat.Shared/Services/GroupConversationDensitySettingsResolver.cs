using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 保存一次群聊规划需要使用的发言人数和消息总量边界。
/// </summary>
public sealed record GroupConversationDensitySettings(
    int MaximumSpeakersPerTurn,
    int WholeGroupMaximumSpeakersPerTurn,
    int MaximumMessagesPerTurn)
{
    private static readonly string[] WholeGroupMarkers =
    [
        "你们",
        "大家",
        "各位",
        "都说说",
        "都聊聊"
    ];

    /// <summary>
    /// 面向全群或明确点名多位好友时使用更适合多人回应的上限。
    /// </summary>
    public int ResolveMaximumSpeakerCount(
        GroupChat groupChat,
        string messageContent)
    {
        ArgumentNullException.ThrowIfNull(groupChat);

        string content = messageContent ?? string.Empty;
        bool addressesWholeGroup = WholeGroupMarkers.Any(marker =>
            content.Contains(marker, StringComparison.OrdinalIgnoreCase));
        int mentionedMemberCount = groupChat.Members.Count(member =>
            content.Contains(
                $"@{member.Nickname}",
                StringComparison.OrdinalIgnoreCase));

        return addressesWholeGroup || mentionedMemberCount > 1
            ? WholeGroupMaximumSpeakersPerTurn
            : MaximumSpeakersPerTurn;
    }
}

/// <summary>
/// 从全局设置中读取群聊密度；没有已保存设置时使用安全默认值。
/// </summary>
public sealed class GroupConversationDensitySettingsResolver
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public GroupConversationDensitySettingsResolver(
        VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public GroupConversationDensitySettings Resolve()
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AutonomousInteractionSettings settings = dbContext
            .AutonomousInteractionSettings
            .AsNoTracking()
            .SingleOrDefault(item =>
                item.Id == AutonomousInteractionSettings.SingletonId)
            ?? new AutonomousInteractionSettings();

        return new GroupConversationDensitySettings(
            settings.GroupChatMaximumSpeakersPerTurn,
            settings.GroupChatWholeGroupMaximumSpeakersPerTurn,
            settings.GroupChatMaximumMessagesPerTurn);
    }
}

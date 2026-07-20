using System;

namespace VocaChat.Models;

/// <summary>
/// 保存当前本地用户对好友自主互动功能的全局设置。
/// </summary>
public class AutonomousInteractionSettings
{
    internal const int SingletonId = 1;
    internal const int DefaultPrivateChatContinuationRatePercent = 80;
    internal const int MinimumPrivateChatContinuationRatePercent = 0;
    internal const int MaximumPrivateChatContinuationRatePercent = 95;
    internal const int DefaultPrivateChatMaximumRounds = 6;
    internal const int MinimumPrivateChatMaximumRounds = 1;
    internal const int MaximumPrivateChatMaximumRounds = 12;
    internal const int DefaultAutonomousGroupChatMaximumMembers = 6;
    internal const int MinimumAutonomousGroupChatMaximumMembers = 3;
    internal const int DefaultGroupChatContinuationRatePercent = 80;
    internal const int MinimumGroupChatContinuationRatePercent = 0;
    internal const int MaximumGroupChatContinuationRatePercent = 95;
    internal const int DefaultGroupChatMaximumRounds = 4;
    internal const int MinimumGroupChatMaximumRounds = 1;
    internal const int MaximumGroupChatMaximumRounds = 12;
    internal const AiReplyDelayMode DefaultReplyDelayMode =
        AiReplyDelayMode.RandomRange;
    internal const long DefaultFixedReplyDelayMilliseconds = 1200;
    internal const long DefaultMinimumReplyDelayMilliseconds = 800;
    internal const long DefaultMaximumReplyDelayMilliseconds = 1800;
    internal const AiReplyDelayMode DefaultConsecutiveMessageDelayMode =
        AiReplyDelayMode.RandomRange;
    internal const long DefaultFixedConsecutiveMessageDelayMilliseconds = 700;
    internal const long DefaultMinimumConsecutiveMessageDelayMilliseconds = 400;
    internal const long DefaultMaximumConsecutiveMessageDelayMilliseconds = 1200;
    internal const int DefaultMaximumConsecutiveQuestionTurns = 2;
    internal const int MinimumMaximumConsecutiveQuestionTurns = 1;

    public int Id { get; private set; }
    public bool IsEnabled { get; private set; }
    public AutonomousInteractionFrequency Frequency { get; private set; }
    public bool AllowPrivateChats { get; private set; }
    public bool AllowGroupChats { get; private set; }
    public int PrivateChatContinuationRatePercent { get; private set; }
    public int PrivateChatMaximumRounds { get; private set; }
    public int AutonomousGroupChatMaximumMembers { get; private set; }
    public int GroupChatContinuationRatePercent { get; private set; }
    public int GroupChatMaximumRounds { get; private set; }
    public AiReplyDelayMode ReplyDelayMode { get; private set; }
    public long FixedReplyDelayMilliseconds { get; private set; }
    public long MinimumReplyDelayMilliseconds { get; private set; }
    public long MaximumReplyDelayMilliseconds { get; private set; }
    public AiReplyDelayMode ConsecutiveMessageDelayMode { get; private set; }
    public long FixedConsecutiveMessageDelayMilliseconds { get; private set; }
    public long MinimumConsecutiveMessageDelayMilliseconds { get; private set; }
    public long MaximumConsecutiveMessageDelayMilliseconds { get; private set; }
    public int MaximumConsecutiveQuestionTurns { get; private set; }

    /// <summary>
    /// 创建尚未保存的默认设置，或供 EF Core 从数据库还原设置。
    /// </summary>
    internal AutonomousInteractionSettings()
    {
        Id = SingletonId;
        IsEnabled = false;
        Frequency = AutonomousInteractionFrequency.Normal;
        AllowPrivateChats = true;
        AllowGroupChats = true;
        PrivateChatContinuationRatePercent =
            DefaultPrivateChatContinuationRatePercent;
        PrivateChatMaximumRounds = DefaultPrivateChatMaximumRounds;
        AutonomousGroupChatMaximumMembers =
            DefaultAutonomousGroupChatMaximumMembers;
        GroupChatContinuationRatePercent =
            DefaultGroupChatContinuationRatePercent;
        GroupChatMaximumRounds = DefaultGroupChatMaximumRounds;
        ReplyDelayMode = DefaultReplyDelayMode;
        FixedReplyDelayMilliseconds = DefaultFixedReplyDelayMilliseconds;
        MinimumReplyDelayMilliseconds = DefaultMinimumReplyDelayMilliseconds;
        MaximumReplyDelayMilliseconds = DefaultMaximumReplyDelayMilliseconds;
        ConsecutiveMessageDelayMode = DefaultConsecutiveMessageDelayMode;
        FixedConsecutiveMessageDelayMilliseconds =
            DefaultFixedConsecutiveMessageDelayMilliseconds;
        MinimumConsecutiveMessageDelayMilliseconds =
            DefaultMinimumConsecutiveMessageDelayMilliseconds;
        MaximumConsecutiveMessageDelayMilliseconds =
            DefaultMaximumConsecutiveMessageDelayMilliseconds;
        MaximumConsecutiveQuestionTurns =
            DefaultMaximumConsecutiveQuestionTurns;
    }

    /// <summary>
    /// 保存已经由 Service 验证通过的全局自主互动设置。
    /// </summary>
    internal void Update(
        bool isEnabled,
        AutonomousInteractionFrequency frequency,
        bool allowPrivateChats,
        bool allowGroupChats,
        int privateChatContinuationRatePercent,
        int privateChatMaximumRounds,
        int autonomousGroupChatMaximumMembers,
        int groupChatContinuationRatePercent,
        int groupChatMaximumRounds,
        AiReplyDelayMode replyDelayMode,
        long fixedReplyDelayMilliseconds,
        long minimumReplyDelayMilliseconds,
        long maximumReplyDelayMilliseconds,
        AiReplyDelayMode consecutiveMessageDelayMode,
        long fixedConsecutiveMessageDelayMilliseconds,
        long minimumConsecutiveMessageDelayMilliseconds,
        long maximumConsecutiveMessageDelayMilliseconds,
        int maximumConsecutiveQuestionTurns)
    {
        IsEnabled = isEnabled;
        Frequency = frequency;
        AllowPrivateChats = allowPrivateChats;
        AllowGroupChats = allowGroupChats;
        PrivateChatContinuationRatePercent =
            privateChatContinuationRatePercent;
        PrivateChatMaximumRounds = privateChatMaximumRounds;
        AutonomousGroupChatMaximumMembers =
            autonomousGroupChatMaximumMembers;
        GroupChatContinuationRatePercent = groupChatContinuationRatePercent;
        GroupChatMaximumRounds = groupChatMaximumRounds;
        ReplyDelayMode = replyDelayMode;
        FixedReplyDelayMilliseconds = fixedReplyDelayMilliseconds;
        MinimumReplyDelayMilliseconds = minimumReplyDelayMilliseconds;
        MaximumReplyDelayMilliseconds = maximumReplyDelayMilliseconds;
        ConsecutiveMessageDelayMode = consecutiveMessageDelayMode;
        FixedConsecutiveMessageDelayMilliseconds =
            fixedConsecutiveMessageDelayMilliseconds;
        MinimumConsecutiveMessageDelayMilliseconds =
            minimumConsecutiveMessageDelayMilliseconds;
        MaximumConsecutiveMessageDelayMilliseconds =
            maximumConsecutiveMessageDelayMilliseconds;
        MaximumConsecutiveQuestionTurns = maximumConsecutiveQuestionTurns;
    }
}

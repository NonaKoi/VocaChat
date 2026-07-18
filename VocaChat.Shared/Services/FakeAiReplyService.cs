using System;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责为已经选定的 AI 发言者生成本地模拟回复。
/// </summary>
public class FakeAiReplyService
{
    /// <summary>
    /// 生成不依赖网络或真实大模型的假 AI 回复文本。
    /// </summary>
    public string GenerateReply(AiAccount aiSpeaker, string userContent)
    {
        string identityDescription = string.IsNullOrWhiteSpace(aiSpeaker.IdentityDescription)
            ? "暂未填写身份描述"
            : aiSpeaker.IdentityDescription;
        string personality = string.IsNullOrWhiteSpace(aiSpeaker.Personality)
            ? "暂未填写性格"
            : aiSpeaker.Personality;
        string speakingStyle = string.IsNullOrWhiteSpace(aiSpeaker.SpeakingStyle)
            ? "暂未填写说话风格"
            : aiSpeaker.SpeakingStyle;

        return $"我是{aiSpeaker.Nickname}，{identityDescription}，性格是{personality}，"
            + $"说话风格是{speakingStyle}。我看到了你刚才说的“{userContent}”。"
            + "这是当前由系统生成的模拟回复。";
    }

    /// <summary>
    /// 为第二位群成员生成区别于主回复的接话文本。
    /// </summary>
    public string GenerateFollowUpReply(
        AiAccount aiSpeaker,
        AiAccount primarySpeaker,
        string userContent)
    {
        string speakingStyle = string.IsNullOrWhiteSpace(aiSpeaker.SpeakingStyle)
            ? "自然地"
            : $"用{aiSpeaker.SpeakingStyle}的方式";

        return $"我是{aiSpeaker.Nickname}。{primarySpeaker.Nickname}刚才的回应我也看到了，"
            + $"我想{speakingStyle}补充一句：关于“{userContent}”，我也有一些想法。"
            + "这是当前由系统生成的模拟跟进回复。";
    }

    /// <summary>
    /// 为一次好友自主私信生成本地模拟开场白。
    /// </summary>
    public string GenerateAutonomousPrivateChatOpening(
        AiAccount initiator,
        AiAccount recipient)
    {
        string topic = initiator.Tags
            .FirstOrDefault(tag => tag.Type == AiAccountTagType.Interest)
            ?.Value
            ?? "最近的生活";

        return $"{recipient.Nickname}，刚好想到你了。最近想和你聊聊{topic}，你这会儿怎么样？";
    }

    /// <summary>
    /// 为好友自主私信生成接收方的本地模拟回复。
    /// </summary>
    public string GenerateAutonomousPrivateChatReply(
        AiAccount recipient,
        AiAccount initiator,
        string openingContent)
    {
        string speakingStyle = string.IsNullOrWhiteSpace(recipient.SpeakingStyle)
            ? "自然地"
            : $"用{recipient.SpeakingStyle}的方式";

        return $"{initiator.Nickname}，收到你的消息了。我会{speakingStyle}回应你："
            + $"关于“{openingContent}”，我也正想和你聊聊。";
    }
}

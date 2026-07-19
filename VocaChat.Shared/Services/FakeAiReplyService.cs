using System;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责为已经选定的 AI 发言者生成本地模拟回复。
/// </summary>
public class FakeAiReplyService : IAiMessageGenerator
{
    /// <summary>
    /// 为测试和离线验证提供与真实生成器相同的异步契约。
    /// </summary>
    public Task<IReadOnlyList<string>> GenerateMessagesAsync(
        AiMessageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ExpectedMessageCount == 0)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        if (request.ActionPlan is null)
        {
            throw new InvalidOperationException("本次 AI 消息缺少行为与表达计划。");
        }

        AiAccount otherParticipant = request.OtherParticipants.FirstOrDefault()
            ?? request.Speaker;
        string targetContent = request.ReplyTarget?.Message?.Content
            ?? request.FocusContent;
        IReadOnlyList<string> messages = request.Scenario switch
        {
            AiMessageGenerationScenario.UserPrivateChat
                => GenerateUserPrivateChatMessages(
                    request.Speaker,
                    targetContent,
                    request.ExpectedMessageCount),
            AiMessageGenerationScenario.GroupPrimaryReply
                => new[] { GenerateReply(request.Speaker, targetContent) },
            AiMessageGenerationScenario.GroupFollowUpReply
                => new[]
                {
                    GenerateFollowUpReply(
                        request.Speaker,
                        request.PrimarySpeaker ?? request.Speaker,
                        targetContent)
                },
            AiMessageGenerationScenario.AutonomousPrivateChat
                => GenerateAutonomousPrivateChatMessages(
                    request.Speaker,
                    otherParticipant,
                    request.Topic,
                    targetContent,
                    request.ExpectedMessageCount,
                    request.RoundNumber ?? 1,
                    request.IsInitiator),
            AiMessageGenerationScenario.AutonomousPrivateChatClosing
                => GenerateAutonomousPrivateChatClosingMessages(
                    request.Speaker,
                    otherParticipant,
                    request.Topic,
                    request.ExpectedMessageCount,
                    request.IsInitiator),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Scenario))
        };

        return Task.FromResult(messages);
    }

    private IReadOnlyList<string> GenerateUserPrivateChatMessages(
        AiAccount speaker,
        string userContent,
        int messageCount)
    {
        if (messageCount is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(messageCount),
                "用户私信回复数量必须在 1 到 3 之间。");
        }

        string[] candidates =
        {
            GenerateReply(speaker, userContent),
            $"关于“{userContent}”，我还想再补充一个相关的想法。",
            "这几条是同一次回应中自然分开发送的消息。"
        };
        return candidates.Take(messageCount).ToList().AsReadOnly();
    }

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
    /// 从发起者已有兴趣中选择本地模拟自主私信使用的话题。
    /// </summary>
    public string GetAutonomousPrivateChatTopic(AiAccount initiator)
    {
        return initiator.Tags
            .FirstOrDefault(tag => tag.Type == AiAccountTagType.Interest)
            ?.Value
            ?? "最近的生活";
    }

    /// <summary>
    /// 为一次好友自主私信生成本地模拟开场白。
    /// </summary>
    public string GenerateAutonomousPrivateChatOpening(
        AiAccount initiator,
        AiAccount recipient)
    {
        string topic = GetAutonomousPrivateChatTopic(initiator);

        return GenerateAutonomousPrivateChatOpening(
            initiator,
            recipient,
            topic);
    }

    /// <summary>
    /// 使用已经保存到 Session 计划中的话题生成本地模拟开场白。
    /// </summary>
    public string GenerateAutonomousPrivateChatOpening(
        AiAccount initiator,
        AiAccount recipient,
        string topic)
    {
        string normalizedTopic = string.IsNullOrWhiteSpace(topic)
            ? "最近的生活"
            : topic.Trim();

        return $"{recipient.Nickname}，刚好想到你了。最近想和你聊聊{normalizedTopic}，你这会儿怎么样？";
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

    /// <summary>
    /// 按轮次计划生成一个参与者的独立消息列表，不通过标点机械拆分长文本。
    /// </summary>
    public IReadOnlyList<string> GenerateAutonomousPrivateChatMessages(
        AiAccount speaker,
        AiAccount otherParticipant,
        string topic,
        string previousMessageContent,
        int messageCount,
        int roundNumber,
        bool isInitiator)
    {
        if (messageCount is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(messageCount),
                "普通轮单方消息数量必须在 0 到 3 之间。");
        }

        if (messageCount == 0)
        {
            return Array.Empty<string>();
        }

        string normalizedTopic = string.IsNullOrWhiteSpace(topic)
            ? "最近的生活"
            : topic.Trim();
        string previousSummary = Summarize(previousMessageContent);
        string[] candidates = isInitiator
            ? roundNumber == 1
                ? new[]
                {
                    $"{otherParticipant.Nickname}，刚好想到你了。最近想和你聊聊{normalizedTopic}。",
                    $"我这两天正好又遇到一点和{normalizedTopic}有关的事。",
                    "想到你可能会有不一样的看法，就来问问你。"
                }
                : new[]
                {
                    $"你刚才说的“{previousSummary}”让我又想到一点。",
                    $"如果继续说{normalizedTopic}，我还挺想听听你的经历。",
                    "不用急着给结论，想到什么就说什么。"
                }
            : new[]
            {
                $"{otherParticipant.Nickname}，收到啦。关于{normalizedTopic}，我确实有些想法。",
                $"你刚才提到“{previousSummary}”，这部分我挺有共鸣的。",
                $"换成我的角度，我可能会{GetSpeakingStyleDescription(speaker)}说得更直接一点。"
            };

        return candidates.Take(messageCount).ToList().AsReadOnly();
    }

    /// <summary>
    /// 为已经确定需要发言的一方生成简短收束消息。
    /// </summary>
    public IReadOnlyList<string> GenerateAutonomousPrivateChatClosingMessages(
        AiAccount speaker,
        AiAccount otherParticipant,
        string topic,
        int messageCount,
        bool isInitiator)
    {
        if (messageCount is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(messageCount),
                "收束轮单方消息数量必须在 0 到 2 之间。");
        }

        if (messageCount == 0)
        {
            return Array.Empty<string>();
        }

        string[] candidates = isInitiator
            ? new[]
            {
                $"那关于{topic}我们先聊到这里，之后想到新的再和你说。",
                $"你先忙吧，{otherParticipant.Nickname}，回头聊。"
            }
            : new[]
            {
                $"好呀，今天聊{topic}挺开心的。",
                $"回头再聊，{otherParticipant.Nickname}。"
            };

        return candidates.Take(messageCount).ToList().AsReadOnly();
    }

    private static string Summarize(string content)
    {
        const int maximumLength = 60;
        string normalizedContent = string.IsNullOrWhiteSpace(content)
            ? "刚才的话题"
            : content.Trim();

        return normalizedContent.Length <= maximumLength
            ? normalizedContent
            : $"{normalizedContent[..maximumLength]}…";
    }

    private static string GetSpeakingStyleDescription(AiAccount speaker)
    {
        return string.IsNullOrWhiteSpace(speaker.SpeakingStyle)
            ? "自然地"
            : $"用{speaker.SpeakingStyle}的方式";
    }
}

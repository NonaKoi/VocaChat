using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证模型上下文使用发送者 Id 而不是显示名判断事实归属。
/// </summary>
public sealed class AiConversationContextBuilderTests
{
    [Fact]
    public void Build_ClassifiesCurrentSpeakerOtherAiAndLocalUser()
    {
        AiAccount speaker = CreateAccount("当前好友");
        AiAccount other = CreateAccount("其他好友");
        AiMessageGenerationRequest request = CreateRequest(
            speaker,
            new AiDialogueMessage(
                "相同显示名",
                "这是当前好友自己说过的事",
                MessageSenderType.AiAccount,
                speaker.Id),
            new AiDialogueMessage(
                "相同显示名",
                "这是另一个好友说过的事",
                MessageSenderType.AiAccount,
                other.Id),
            new AiDialogueMessage(
                "我",
                "这是本地用户说过的事",
                MessageSenderType.User,
                null));

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(request, recentMessageLimit: 24);

        Assert.Equal(3, context.Messages.Count);
        Assert.Equal(
            AiConversationMessageOwnership.CurrentSpeaker,
            context.Messages[0].Ownership);
        Assert.Equal(
            AiConversationMessageOwnership.OtherAiAccount,
            context.Messages[1].Ownership);
        Assert.Equal(
            AiConversationMessageOwnership.LocalUser,
            context.Messages[2].Ownership);
    }

    [Fact]
    public void Build_AppliesLimitWithoutChangingRemainingOrder()
    {
        AiAccount speaker = CreateAccount("当前好友");
        AiMessageGenerationRequest request = CreateRequest(
            speaker,
            new AiDialogueMessage(
                "我",
                "第一条",
                MessageSenderType.User,
                null),
            new AiDialogueMessage(
                speaker.Nickname,
                "第二条",
                MessageSenderType.AiAccount,
                speaker.Id),
            new AiDialogueMessage(
                "我",
                "第三条",
                MessageSenderType.User,
                null));

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(request, recentMessageLimit: 2);

        Assert.Equal(
            new[] { "第二条", "第三条" },
            context.Messages.Select(item => item.Message.Content));
    }

    [Fact]
    public void Build_SeparatesReplyTargetFromBackgroundMessages()
    {
        AiAccount speaker = CreateAccount("当前好友");
        AiDialogueMessage oldMessage = new(
            "我",
            "这是更早的旧话题",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiDialogueMessage targetMessage = new(
            "我",
            "这才是当前必须回应的问题？",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = CreateRequest(
            speaker,
            oldMessage,
            targetMessage) with
        {
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage)
        };

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(request, recentMessageLimit: 12);

        Assert.NotNull(context.ReplyTarget);
        Assert.Equal(targetMessage.MessageId, context.ReplyTarget.Message.MessageId);
        Assert.Equal(
            AiConversationMessageOwnership.LocalUser,
            context.ReplyTarget.Ownership);
        Assert.Single(context.Messages);
        Assert.Equal(oldMessage.MessageId, context.Messages[0].Message.MessageId);
    }

    [Fact]
    public void Build_KeepsOnlyCurrentSpeakerMemoriesAboutCurrentParticipants()
    {
        AiAccount speaker = CreateAccount("当前好友");
        AiAccount other = CreateAccount("对话好友");
        AiAccount unrelated = CreateAccount("无关好友");
        DateTime occurredAt = new(2026, 7, 18, 9, 0, 0);
        List<AiConversationMemory> candidates = Enumerable
            .Range(1, 5)
            .Select(index => new AiConversationMemory(
                speaker.Id,
                other.Id,
                other.Nickname,
                AiMemoryType.Preference,
                $"正确方向记忆{index}",
                occurredAt.AddMinutes(index)))
            .ToList();
        candidates.Add(new AiConversationMemory(
            other.Id,
            speaker.Id,
            speaker.Nickname,
            AiMemoryType.Preference,
            "反向记忆",
            occurredAt));
        candidates.Add(new AiConversationMemory(
            speaker.Id,
            unrelated.Id,
            unrelated.Nickname,
            AiMemoryType.Habit,
            "无关对象记忆",
            occurredAt));

        AiMessageGenerationRequest request = CreateRequest(speaker) with
        {
            OtherParticipants = new[] { other },
            RelevantMemories = candidates
        };

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(request, recentMessageLimit: 12);

        Assert.Equal(4, context.Memories.Count);
        Assert.Equal(
            new[]
            {
                "正确方向记忆1",
                "正确方向记忆2",
                "正确方向记忆3",
                "正确方向记忆4"
            },
            context.Memories.Select(memory => memory.Summary));
        Assert.DoesNotContain(
            context.Memories,
            memory => memory.OwnerAiAccountId != speaker.Id
                || memory.SubjectAiAccountId != other.Id);
    }

    private static AiMessageGenerationRequest CreateRequest(
        AiAccount speaker,
        params AiDialogueMessage[] messages)
    {
        return new AiMessageGenerationRequest
        {
            Scenario = AiMessageGenerationScenario.UserPrivateChat,
            Speaker = speaker,
            RecentMessages = messages,
            ExpectedMessageCount = 1
        };
    }

    private static AiAccount CreateAccount(string nickname)
    {
        return new AiAccount(
            Guid.NewGuid().ToString("N")[..7],
            nickname,
            string.Empty,
            string.Empty,
            string.Empty);
    }
}

using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

public sealed class ConversationQuestionPolicyServiceTests
{
    [Fact]
    public void ConsecutiveQuestionTurns_ReachingConfiguredLimit_ForcesStatement()
    {
        Guid speakerId = Guid.NewGuid();
        IReadOnlyList<AiDialogueMessage> messages = new[]
        {
            Message(speakerId, "你今天忙吗？"),
            Message(null, "还好。", MessageSenderType.User),
            Message(speakerId, "那晚上有安排吗？"),
            Message(null, "暂时没有。", MessageSenderType.User)
        };

        int streak = ConversationQuestionPolicyService
            .CountConsecutiveQuestionTurns(speakerId, messages);
        ConversationQuestionPolicy policy = new(streak, 2);

        Assert.Equal(2, streak);
        Assert.True(policy.ForceDeclarativeReply);
        ConversationActionPlan applied = policy.ApplyTo(CreateAskPlan());
        Assert.Equal(ConversationAction.Acknowledge, applied.Action);
        Assert.Equal(ConversationQuestionMode.None, applied.QuestionMode);
    }

    [Fact]
    public void DeclarativeTurn_ResetsQuestionStreak()
    {
        Guid speakerId = Guid.NewGuid();
        IReadOnlyList<AiDialogueMessage> messages = new[]
        {
            Message(speakerId, "你今天忙吗？"),
            Message(null, "还好。", MessageSenderType.User),
            Message(speakerId, "那就晚点再聊。"),
            Message(null, "好。", MessageSenderType.User)
        };

        Assert.Equal(
            0,
            ConversationQuestionPolicyService.CountConsecutiveQuestionTurns(
                speakerId,
                messages));
    }

    private static AiDialogueMessage Message(
        Guid? senderAiAccountId,
        string content,
        MessageSenderType senderType = MessageSenderType.AiAccount) =>
        new("测试发送者", content, senderType, senderAiAccountId);

    private static ConversationActionPlan CreateAskPlan() =>
        new(
            ConversationAction.Ask,
            ConversationMessageLength.Short,
            ConversationDirectness.Direct,
            ConversationQuestionMode.Natural,
            ConversationEmotionVisibility.Natural,
            ConversationTopicMovement.Stay,
            ConversationPunctuationRhythm.Natural,
            ConversationRelationshipTone.Familiar,
            ConversationRelationshipBalance.Balanced,
            MayOmitObviousContext: false,
            MayLeaveThoughtOpen: false);
}


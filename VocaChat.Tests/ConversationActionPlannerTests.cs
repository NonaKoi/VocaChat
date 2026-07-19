using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证生成前行为计划不会改变现有发言规则，并能表达场景、人物和关系差异。
/// </summary>
public sealed class ConversationActionPlannerTests
{
    [Fact]
    public void ClosingScenario_AlwaysUsesCloseActionWithoutQuestion()
    {
        ConversationActionPlanner planner = new(new ConstantRandom(0.9));

        ConversationActionPlan plan = planner.CreatePlan(CreateRequest(
            AiMessageGenerationScenario.AutonomousPrivateChatClosing));

        Assert.Equal(ConversationAction.Close, plan.Action);
        Assert.Equal(ConversationQuestionMode.None, plan.QuestionMode);
        Assert.Equal(ConversationTopicMovement.Stay, plan.TopicMovement);
    }

    [Theory]
    [InlineData(20, ConversationRelationshipTone.Distant)]
    [InlineData(40, ConversationRelationshipTone.Reserved)]
    [InlineData(60, ConversationRelationshipTone.Familiar)]
    [InlineData(85, ConversationRelationshipTone.Close)]
    public void RelationshipScore_IsConvertedToBehavioralTone(
        double relationshipScore,
        ConversationRelationshipTone expectedTone)
    {
        ConversationActionPlanner planner = new(new ConstantRandom(0.2));
        AiMessageGenerationRequest request = CreateRequest(
            AiMessageGenerationScenario.AutonomousPrivateChat) with
        {
            SpeakerToOtherRelationshipScore = relationshipScore,
            OtherToSpeakerRelationshipScore = relationshipScore
        };

        ConversationActionPlan plan = planner.CreatePlan(request);

        Assert.Equal(expectedTone, plan.RelationshipTone);
        Assert.Equal(
            ConversationRelationshipBalance.Balanced,
            plan.RelationshipBalance);
    }

    [Fact]
    public void UnequalRelationship_KeepsDirectionalDifference()
    {
        ConversationActionPlanner planner = new(new ConstantRandom(0.2));
        AiMessageGenerationRequest request = CreateRequest(
            AiMessageGenerationScenario.AutonomousPrivateChat) with
        {
            SpeakerToOtherRelationshipScore = 80,
            OtherToSpeakerRelationshipScore = 45
        };

        ConversationActionPlan plan = planner.CreatePlan(request);

        Assert.Equal(
            ConversationRelationshipBalance.SpeakerMoreInvested,
            plan.RelationshipBalance);
    }

    [Fact]
    public void QuietSpeaker_ReceivesBriefRestrainedExpressionPlan()
    {
        AiAccount quietSpeaker = new(
            "7654321",
            "小静",
            string.Empty,
            "安静内向",
            "寡言");
        ConversationActionPlanner planner = new(new ConstantRandom(0.4));
        AiMessageGenerationRequest request = CreateRequest(
            AiMessageGenerationScenario.UserPrivateChat) with
        {
            Speaker = quietSpeaker
        };

        ConversationActionPlan plan = planner.CreatePlan(request);

        Assert.Equal(ConversationMessageLength.VeryShort, plan.MessageLength);
        Assert.Equal(
            ConversationEmotionVisibility.Restrained,
            plan.EmotionVisibility);
        Assert.Equal(
            ConversationPunctuationRhythm.Sparse,
            plan.PunctuationRhythm);
    }

    [Fact]
    public void ApplyPlan_DoesNotChangeSpeakerOrExpectedMessageCount()
    {
        AiMessageGenerationRequest request = CreateRequest(
            AiMessageGenerationScenario.GroupPrimaryReply) with
        {
            ExpectedMessageCount = 2
        };
        ConversationActionPlanner planner = new(new ConstantRandom(0.3));

        AiMessageGenerationRequest plannedRequest = planner.ApplyPlan(request);

        Assert.Same(request.Speaker, plannedRequest.Speaker);
        Assert.Equal(2, plannedRequest.ExpectedMessageCount);
        Assert.NotNull(plannedRequest.ActionPlan);
    }

    [Fact]
    public void DirectQuestionReplyTarget_AlwaysUsesAnswerAction()
    {
        ConversationActionPlanner planner = new(new ConstantRandom(0.99));
        AiDialogueMessage targetMessage = new(
            "我",
            "你今天几点回来",
            MessageSenderType.User,
            null);
        AiMessageGenerationRequest request = CreateRequest(
            AiMessageGenerationScenario.UserPrivateChat) with
        {
            FocusContent = targetMessage.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage)
        };

        ConversationActionPlan plan = planner.CreatePlan(request);

        Assert.Equal(ConversationAction.Answer, plan.Action);
        Assert.Equal(ConversationDirectness.Direct, plan.Directness);
        Assert.Equal(ConversationQuestionMode.None, plan.QuestionMode);
        Assert.False(plan.MayLeaveThoughtOpen);
    }

    [Fact]
    public void DetailedUserQuestion_UsesModerateCompleteAnswerPlan()
    {
        ConversationActionPlanner planner = new(new ConstantRandom(0.99));
        AiDialogueMessage targetMessage = new(
            "我",
            "你最近具体在忙什么，详细讲讲",
            MessageSenderType.User,
            null);
        AiMessageGenerationRequest request = CreateRequest(
            AiMessageGenerationScenario.UserPrivateChat) with
        {
            FocusContent = targetMessage.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage)
        };

        ConversationActionPlan plan = planner.CreatePlan(request);

        Assert.Equal(ConversationAction.Answer, plan.Action);
        Assert.Equal(ConversationMessageLength.Moderate, plan.MessageLength);
        Assert.False(plan.MayLeaveThoughtOpen);
    }

    [Fact]
    public void DirectRequestReplyTarget_AlwaysUsesAnswerAction()
    {
        ConversationActionPlanner planner = new(new ConstantRandom(0.99));
        AiDialogueMessage targetMessage = new(
            "我",
            "连贯性验收：请说一种喜欢的天气",
            MessageSenderType.User,
            null);
        AiMessageGenerationRequest request = CreateRequest(
            AiMessageGenerationScenario.GroupPrimaryReply) with
        {
            FocusContent = targetMessage.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage)
        };

        ConversationActionPlan plan = planner.CreatePlan(request);

        Assert.Equal(ConversationAction.Answer, plan.Action);
        Assert.Equal(ConversationDirectness.Direct, plan.Directness);
    }

    private static AiMessageGenerationRequest CreateRequest(
        AiMessageGenerationScenario scenario)
    {
        return new AiMessageGenerationRequest
        {
            Scenario = scenario,
            Speaker = new AiAccount(
                "1234567",
                "小语",
                "喜欢安静聊天的朋友",
                "温和",
                "简短自然"),
            FocusContent = "今天有点累",
            RecentMessages = new[]
            {
                new AiDialogueMessage(
                    "朋友",
                    "今天有点累",
                    MessageSenderType.User,
                    null)
            },
            ExpectedMessageCount = 1
        };
    }

    private sealed class ConstantRandom : Random
    {
        private readonly double _value;

        public ConstantRandom(double value)
        {
            _value = value;
        }

        protected override double Sample()
        {
            return _value;
        }
    }
}

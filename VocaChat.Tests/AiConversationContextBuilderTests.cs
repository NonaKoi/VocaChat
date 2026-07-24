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
        Assert.Equal(
            AiConversationFactUsage.SpeakerNarrative,
            context.Messages[0].FactUsage);
        Assert.Equal(
            AiConversationFactUsage.HearsayOnly,
            context.Messages[1].FactUsage);
        Assert.Equal(
            AiConversationFactUsage.UserProvidedContext,
            context.Messages[2].FactUsage);
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
    public void Build_DistinguishesReplyTargetAiFromOtherGroupMembers()
    {
        AiAccount speaker = CreateAccount("当前好友");
        AiAccount target = CreateAccount("回应对象");
        AiAccount thirdParty = CreateAccount("第三方好友");
        AiDialogueMessage targetMessage = new(
            target.Nickname,
            "我昨天去看了海边",
            MessageSenderType.AiAccount,
            target.Id,
            Guid.NewGuid());
        AiDialogueMessage thirdPartyMessage = new(
            thirdParty.Nickname,
            "我最近在学陶艺",
            MessageSenderType.AiAccount,
            thirdParty.Id,
            Guid.NewGuid());
        AiMessageGenerationRequest request = CreateRequest(
            speaker,
            targetMessage,
            thirdPartyMessage) with
        {
            OtherParticipants = new[] { target, thirdParty },
            RelationshipTarget = target,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage)
        };

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(request, recentMessageLimit: 12);

        Assert.Equal(
            AiConversationMessageOwnership.ReplyTargetAiAccount,
            context.ReplyTarget!.Ownership);
        AiConversationContextMessage thirdPartyContext = Assert.Single(
            context.Messages);
        Assert.Equal(
            AiConversationMessageOwnership.OtherAiAccount,
            thirdPartyContext.Ownership);
    }

    [Fact]
    public void Build_WithoutPreparedWorldContext_DoesNotInferCrossWorldStatus()
    {
        CharacterWorld speakerWorld = new(
            "沙海学园都市",
            "学园与沙漠并存的世界。");
        CharacterWorld otherWorld = new(
            "雾海列岛",
            "群岛漂浮在常年雾海之上。");
        AiAccount speaker = CreateAccount("当前好友");
        speaker.AssignCharacterWorld(speakerWorld);
        AiAccount other = CreateAccount("异世界好友");
        other.AssignCharacterWorld(otherWorld);
        AiDialogueMessage otherMessage = new(
            other.Nickname,
            "雾海今天很安静",
            MessageSenderType.AiAccount,
            other.Id);
        AiMessageGenerationRequest request = CreateRequest(
            speaker,
            otherMessage) with
        {
            OtherParticipants = new[] { other },
            RelationshipTarget = other
        };

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(request, recentMessageLimit: 12);

        AiConversationContextMessage message = Assert.Single(
            context.Messages);
        Assert.Equal(other.Id, message.FactOwnerAiAccountId);
        Assert.Equal(otherWorld.Id, message.CharacterWorldId);
        Assert.Equal(AiConversationFactUsage.HearsayOnly, message.FactUsage);
        Assert.Empty(context.CrossWorldAiAccountIds);
        Assert.Null(context.WorldConversationContext);
        Assert.Null(context.GroupWorldConversationContext);
    }

    [Fact]
    public void Build_CalculatesGapFromPreviousMessageToReplyTarget()
    {
        AiAccount speaker = CreateAccount("当前好友");
        DateTime targetSentAt = new(2026, 7, 22, 20, 0, 0);
        AiDialogueMessage oldMessage = new(
            speaker.Nickname,
            "前两天聊到咖啡馆老板",
            MessageSenderType.AiAccount,
            speaker.Id,
            Guid.NewGuid(),
            targetSentAt.AddDays(-2));
        AiDialogueMessage targetMessage = new(
            "我",
            "那个人后来怎么样？",
            MessageSenderType.User,
            null,
            Guid.NewGuid(),
            targetSentAt);
        AiMessageGenerationRequest request = CreateRequest(
            speaker,
            oldMessage,
            targetMessage) with
        {
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage)
        };

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(request, recentMessageLimit: 12);

        Assert.Equal(TimeSpan.FromDays(2), context.GapSincePreviousMessage);
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
            RelationshipTarget = other,
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

    [Fact]
    public void Build_WorldKnowledgeKeepsOnlyCurrentSpeakerAndCurrentSubject()
    {
        AiAccount speaker = CreateAccount("当前好友");
        AiAccount subject = CreateAccount("对话好友");
        AiAccount unrelated = CreateAccount("无关好友");
        CharacterWorld world = new(
            "目标世界",
            "只用于世界知识隔离测试。");
        subject.AssignCharacterWorld(world);
        DateTime now = DateTime.Now;
        AiConversationWorldKnowledge valid = new(
            Guid.NewGuid(),
            speaker.Id,
            world.Id,
            subject.Id,
            "对话好友提到的有效知识",
            AiWorldKnowledgeTrustLevel.DirectStatement,
            80,
            now);
        AiConversationWorldKnowledge wrongOwner = valid with
        {
            Id = Guid.NewGuid(),
            OwnerAiAccountId = unrelated.Id,
            Summary = "其他账号持有的知识"
        };
        AiConversationWorldKnowledge wrongSubject = valid with
        {
            Id = Guid.NewGuid(),
            SubjectAiAccountId = unrelated.Id,
            Summary = "其他对象的知识"
        };
        AiMessageGenerationRequest request = CreateRequest(speaker) with
        {
            OtherParticipants = new[] { subject },
            RelationshipTarget = subject,
            WorldConversationContext = new AiWorldConversationContext(
                AiParallelWorldAwarenessState.Unaware,
                AiWorldAwarenessState.AnomalyObserved,
                subject.Id,
                world.Id,
                VisibleSubjectWorldName: "不应显示的世界名",
                IsNewlyInformedByCurrentMessage: false,
                AiWorldInquiryMode.ClarifyUnfamiliarConcept,
                new[] { valid, wrongOwner, wrongSubject })
        };

        AiWorldConversationContext context = Assert.IsType<
            AiWorldConversationContext>(
            new AiConversationContextBuilder()
                .Build(request, recentMessageLimit: 12)
                .WorldConversationContext);

        Assert.Equal(
            valid.Id,
            Assert.Single(context.RelevantKnowledge).Id);
        Assert.Null(context.VisibleSubjectWorldName);
        Assert.Empty(
            new AiConversationContextBuilder()
                .Build(
                    request with
                    {
                        WorldConversationContext =
                            request.WorldConversationContext! with
                            {
                                RelationshipAwareness =
                                    AiWorldAwarenessState.AssumedSharedWorld
                            }
                    },
                    recentMessageLimit: 12)
                .WorldConversationContext!
                .RelevantKnowledge);
    }

    [Fact]
    public void Build_GroupWorldContext_FiltersWrongOwnerAndOutsideParticipant()
    {
        AiAccount speaker = CreateAccount("群聊发言者");
        AiAccount subject = CreateAccount("群内对象");
        AiAccount outside = CreateAccount("群外对象");
        CharacterWorld subjectWorld = new(
            "群内对象世界",
            "用于群聊上下文过滤测试。");
        subject.AssignCharacterWorld(subjectWorld);
        DateTime now = DateTime.Now;
        AiConversationWorldKnowledge valid = new(
            Guid.NewGuid(),
            speaker.Id,
            subjectWorld.Id,
            subject.Id,
            "群内对象在对话中提到的有效知识",
            AiWorldKnowledgeTrustLevel.DirectStatement,
            80,
            now);
        AiConversationWorldKnowledge wrongOwner = valid with
        {
            Id = Guid.NewGuid(),
            OwnerAiAccountId = outside.Id,
            Summary = "群外账号持有的知识"
        };
        AiWorldConversationContext validParticipantContext = new(
            AiParallelWorldAwarenessState.Informed,
            AiWorldAwarenessState.AnomalyObserved,
            subject.Id,
            subjectWorld.Id,
            VisibleSubjectWorldName: "不应显示的世界名",
            IsNewlyInformedByCurrentMessage: false,
            AiWorldInquiryMode.ExploreBackgroundDifference,
            new[] { valid, wrongOwner });
        AiWorldConversationContext outsideContext =
            validParticipantContext with
            {
                SubjectAiAccountId = outside.Id,
                SubjectCharacterWorldId = outside.CharacterWorldId,
                RelevantKnowledge = Array.Empty<
                    AiConversationWorldKnowledge>()
            };
        AiMessageGenerationRequest request = CreateRequest(speaker) with
        {
            Scenario = AiMessageGenerationScenario.GroupPrimaryReply,
            OtherParticipants = new[] { subject },
            GroupWorldConversationContext =
                new AiGroupWorldConversationContext(
                    AiParallelWorldAwarenessState.Informed,
                    IsNewlyInformedByCurrentMessage: false,
                    new[] { validParticipantContext, outsideContext })
        };

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(request, recentMessageLimit: 12);

        AiGroupWorldConversationContext groupContext =
            Assert.IsType<AiGroupWorldConversationContext>(
                context.GroupWorldConversationContext);
        AiWorldConversationContext participantContext = Assert.Single(
            groupContext.ParticipantContexts);
        Assert.Equal(subject.Id, participantContext.SubjectAiAccountId);
        Assert.Equal(
            valid.Id,
            Assert.Single(participantContext.RelevantKnowledge).Id);
        Assert.Null(participantContext.VisibleSubjectWorldName);
        Assert.DoesNotContain(
            outside.Id,
            context.CrossWorldAiAccountIds);
    }

    [Fact]
    public void Build_PrioritizesProtectedSelfFactBeforeMemoryLimit()
    {
        AiAccount speaker = CreateAccount("事实优先好友");
        List<AiConversationSelfMemory> memories = Enumerable
            .Range(0, 12)
            .Select(index => new AiConversationSelfMemory(
                Guid.NewGuid(),
                speaker.Id,
                AiSelfMemoryType.Preference,
                $"一般上下文记忆 {index}",
                $"preference.context-{index}",
                AiSelfMemoryFactNature.Subjective,
                AiSelfMemoryMutability.Mutable,
                AiSelfMemoryTrustLevel.SubjectiveState,
                speaker.CharacterWorldId,
                AiSelfMemorySource.Director,
                90 - index,
                false,
                null,
                new DateTime(2026, 7, 24).AddMinutes(index)))
            .ToList();
        AiConversationSelfMemory protectedFact = new(
            Guid.NewGuid(),
            speaker.Id,
            AiSelfMemoryType.Experience,
            "导师在一次大回潮中失踪",
            "identity.mentor-disappearance",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Immutable,
            AiSelfMemoryTrustLevel.UserCanon,
            speaker.CharacterWorldId,
            AiSelfMemorySource.User,
            100,
            true,
            null,
            new DateTime(2026, 7, 24));
        memories.Add(protectedFact);

        AiConversationContext context = new AiConversationContextBuilder()
            .Build(
                CreateRequest(speaker) with
                {
                    RelevantSelfMemories = memories
                },
                recentMessageLimit: 12);

        Assert.Equal(12, context.SelfMemories.Count);
        Assert.Equal(protectedFact.Id, context.SelfMemories[0].Id);
        Assert.True(context.SelfMemories[0].IsProtectedFact);
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

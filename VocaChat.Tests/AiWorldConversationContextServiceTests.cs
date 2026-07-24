using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证私聊提示词上下文只使用当前发言者已经形成的方向性世界认知。
/// </summary>
public sealed class AiWorldConversationContextServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void PrepareGenerationRequest_InitialContactDoesNotRevealSystemWorld()
    {
        CharacterWorld otherWorld = CreateWorld(
            "基沃托斯",
            "由多个学院自治区组成的学园都市。");
        AiAccount observer = CreateAccount(
            "初识观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount subject = CreateAccount("初识对象", otherWorld.Id);

        AiMessageGenerationRequest prepared = CreateContextService()
            .PrepareGenerationRequest(CreateRequest(
                observer,
                subject,
                "你好，第一次聊天。"));

        AiWorldConversationContext context = Assert.IsType<
            AiWorldConversationContext>(
            prepared.WorldConversationContext);
        Assert.Equal(
            AiWorldAwarenessState.AssumedSharedWorld,
            context.RelationshipAwareness);
        Assert.Equal(
            AiParallelWorldAwarenessState.Unaware,
            context.ParallelWorldAwareness);
        Assert.Null(context.VisibleSubjectWorldName);
        Assert.Empty(context.RelevantKnowledge);
        Assert.Equal(AiWorldInquiryMode.None, context.InquiryMode);
    }

    [Fact]
    public async Task PrepareGenerationRequest_AnomalyRecallsOnlyRelevantKnowledge()
    {
        CharacterWorld otherWorld = CreateWorld(
            "基沃托斯",
            "由多个学院自治区组成的学园都市。");
        AiAccount observer = CreateAccount(
            "知识观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount subject = CreateAccount("知识来源", otherWorld.Id);
        PrivateChat chat = CreateAiPrivateChat(observer, subject);
        PrivateMessage knowledgeMessage = SavePrivateMessage(
            chat,
            subject,
            "阿拜多斯是一所受到沙漠化影响的高中。");
        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            (await CreateProcessor().ProcessPrivateMessageAsync(
                knowledgeMessage.Id)).Status);

        AiMessageGenerationRequest relevant = CreateContextService()
            .PrepareGenerationRequest(CreateRequest(
                observer,
                subject,
                "阿拜多斯高中现在的情况怎么样？"));
        AiMessageGenerationRequest unrelated = CreateContextService()
            .PrepareGenerationRequest(CreateRequest(
                observer,
                subject,
                "今晚吃什么？"));

        AiWorldConversationContext relevantContext = Assert.IsType<
            AiWorldConversationContext>(
            relevant.WorldConversationContext);
        Assert.Equal(
            AiWorldAwarenessState.AnomalyObserved,
            relevantContext.RelationshipAwareness);
        Assert.Null(relevantContext.VisibleSubjectWorldName);
        Assert.Equal(
            AiWorldInquiryMode.ExploreBackgroundDifference,
            relevantContext.InquiryMode);
        Assert.Contains(
            "阿拜多斯",
            Assert.Single(relevantContext.RelevantKnowledge).Summary);
        Assert.Empty(Assert.IsType<AiWorldConversationContext>(
            unrelated.WorldConversationContext).RelevantKnowledge);
    }

    [Fact]
    public async Task PrepareGenerationRequest_KnowledgeIsDirectional()
    {
        CharacterWorld otherWorld = CreateWorld(
            "方向世界",
            "存在特殊学院制度。");
        AiAccount observer = CreateAccount(
            "方向观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount subject = CreateAccount("方向来源", otherWorld.Id);
        PrivateChat chat = CreateAiPrivateChat(observer, subject);
        PrivateMessage message = SavePrivateMessage(
            chat,
            subject,
            "远山学院是一所寄宿学校。");
        await CreateProcessor().ProcessPrivateMessageAsync(message.Id);

        AiWorldConversationContext observerContext = Assert.IsType<
            AiWorldConversationContext>(
            CreateContextService()
                .PrepareGenerationRequest(CreateRequest(
                    observer,
                    subject,
                    "远山学院是什么？"))
                .WorldConversationContext);
        AiWorldConversationContext sourceContext = Assert.IsType<
            AiWorldConversationContext>(
            CreateContextService()
                .PrepareGenerationRequest(CreateRequest(
                    subject,
                    observer,
                    "远山学院是什么？"))
                .WorldConversationContext);

        Assert.Single(observerContext.RelevantKnowledge);
        Assert.Empty(sourceContext.RelevantKnowledge);
        Assert.Equal(
            AiWorldAwarenessState.AssumedSharedWorld,
            sourceContext.RelationshipAwareness);
    }

    [Fact]
    public async Task PrepareGenerationRequest_ExplicitConfirmationEnablesLearnedWorldName()
    {
        CharacterWorld otherWorld = CreateWorld(
            "基沃托斯",
            "由多个学院自治区组成的学园都市。");
        AiAccount observer = CreateAccount(
            "确认观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount subject = CreateAccount("确认来源", otherWorld.Id);
        PrivateChat chat = CreateAiPrivateChat(observer, subject);
        PrivateMessage confirmation = SavePrivateMessage(
            chat,
            subject,
            "基沃托斯是真实存在的另一个世界，我们不在同一个世界，"
            + "现在可以跨世界通信。");
        await CreateProcessor().ProcessPrivateMessageAsync(confirmation.Id);

        AiWorldConversationContext currentContext = Assert.IsType<
            AiWorldConversationContext>(
            CreateContextService()
                .PrepareGenerationRequest(CreateRequest(
                    observer,
                    subject,
                    confirmation.Content,
                    confirmation))
                .WorldConversationContext);
        PrivateMessage later = SavePrivateMessage(
            chat,
            subject,
            "晚上好。");
        AiWorldConversationContext laterContext = Assert.IsType<
            AiWorldConversationContext>(
            CreateContextService()
                .PrepareGenerationRequest(CreateRequest(
                    observer,
                    subject,
                    later.Content,
                    later))
                .WorldConversationContext);

        Assert.Equal(
            AiWorldAwarenessState.CrossWorldConfirmed,
            currentContext.RelationshipAwareness);
        Assert.Equal(
            AiParallelWorldAwarenessState.Informed,
            currentContext.ParallelWorldAwareness);
        Assert.True(currentContext.IsNewlyInformedByCurrentMessage);
        Assert.Equal("基沃托斯", currentContext.VisibleSubjectWorldName);
        Assert.Equal(
            AiWorldInquiryMode.DiscussConfirmedWorld,
            currentContext.InquiryMode);
        Assert.False(laterContext.IsNewlyInformedByCurrentMessage);
        Assert.Equal("基沃托斯", laterContext.VisibleSubjectWorldName);
        Assert.Empty(laterContext.RelevantKnowledge);
    }

    [Fact]
    public void PrepareGenerationRequest_ConfirmationAloneDoesNotRevealUnlearnedWorldName()
    {
        CharacterWorld otherWorld = CreateWorld(
            "未公开世界名",
            "该名称没有在任何对话中出现。");
        AiAccount observer = CreateAccount(
            "未获名称观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount subject = CreateAccount(
            "未公开名称来源",
            otherWorld.Id);
        AiWorldAwarenessService awarenessService =
            new(_database.CreateDbContextFactory());
        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            awarenessService.TrySetWorldAwarenessByUser(
                observer.Id,
                subject.Id,
                AiWorldAwarenessState.CrossWorldConfirmed,
                isUserLocked: false,
                out _,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);

        AiWorldConversationContext context = Assert.IsType<
            AiWorldConversationContext>(
            CreateContextService()
                .PrepareGenerationRequest(CreateRequest(
                    observer,
                    subject,
                    "我们继续聊刚才的事情。"))
                .WorldConversationContext);

        Assert.Equal(
            AiWorldAwarenessState.CrossWorldConfirmed,
            context.RelationshipAwareness);
        Assert.Null(context.VisibleSubjectWorldName);
    }

    [Fact]
    public async Task PrepareGenerationRequest_UserMessageOnlyUpdatesMetaAwareness()
    {
        AiAccount account = CreateAccount(
            "用户私聊好友",
            CharacterWorld.DefaultWorldId);
        Contact contact = Assert.IsType<Contact>(
            new ContactService(_database.CreateDbContextFactory())
                .FindByAiAccountId(account.Id));
        PrivateChatService privateChatService =
            new(_database.CreateDbContextFactory());
        Assert.True(
            privateChatService.TryGetOrCreate(
                contact.Id,
                out PrivateChat? chat,
                out _,
                out string chatError),
            chatError);
        Assert.True(
            privateChatService.TrySaveUserMessage(
                Assert.IsType<PrivateChat>(chat),
                "平行世界真实存在，我们现在正在跨世界通信。",
                out PrivateMessage? message,
                out string saveError),
            saveError);
        PrivateMessage storedMessage = Assert.IsType<PrivateMessage>(message);
        await CreateProcessor().ProcessPrivateMessageAsync(storedMessage.Id);
        AiMessageGenerationRequest request = new()
        {
            Scenario = AiMessageGenerationScenario.UserPrivateChat,
            Speaker = account,
            FocusContent = storedMessage.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(
                ToDialogueMessage(storedMessage)),
            RecentMessages = new[] { ToDialogueMessage(storedMessage) }
        };

        AiWorldConversationContext context = Assert.IsType<
            AiWorldConversationContext>(
            CreateContextService()
                .PrepareGenerationRequest(request)
                .WorldConversationContext);

        Assert.Equal(
            AiParallelWorldAwarenessState.Informed,
            context.ParallelWorldAwareness);
        Assert.Equal(
            AiWorldAwarenessState.AssumedSharedWorld,
            context.RelationshipAwareness);
        Assert.True(context.IsNewlyInformedByCurrentMessage);
        Assert.Null(context.SubjectAiAccountId);
        Assert.Empty(context.RelevantKnowledge);
    }

    [Fact]
    public async Task PrepareGroupGenerationRequest_RecallsKnowledgePerSpeakerOwner()
    {
        CharacterWorld sourceWorld = CreateWorld(
            "群聊来源世界",
            "存在一所名为远山学院的寄宿学校。");
        AiAccount informedSpeaker = CreateAccount(
            "已知情成员",
            CharacterWorld.DefaultWorldId);
        AiAccount uninformedSpeaker = CreateAccount(
            "未知情成员",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("世界来源成员", sourceWorld.Id);
        PrivateChat privateChat = CreateAiPrivateChat(
            informedSpeaker,
            source);
        PrivateMessage evidence = SavePrivateMessage(
            privateChat,
            source,
            "远山学院是一所寄宿学校。");
        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            (await CreateProcessor().ProcessPrivateMessageAsync(
                evidence.Id)).Status);

        AiMessageGenerationRequest informedRequest =
            CreateGroupRequest(
                informedSpeaker,
                new[] { source, uninformedSpeaker },
                "远山学院平时是什么样？");
        AiMessageGenerationRequest uninformedRequest =
            CreateGroupRequest(
                uninformedSpeaker,
                new[] { source, informedSpeaker },
                "远山学院平时是什么样？");

        AiGroupWorldConversationContext informedContext =
            Assert.IsType<AiGroupWorldConversationContext>(
                CreateContextService()
                    .PrepareGroupGenerationRequest(informedRequest)
                    .GroupWorldConversationContext);
        AiGroupWorldConversationContext uninformedContext =
            Assert.IsType<AiGroupWorldConversationContext>(
                CreateContextService()
                    .PrepareGroupGenerationRequest(uninformedRequest)
                    .GroupWorldConversationContext);

        AiWorldConversationContext informedSource =
            Assert.IsType<AiWorldConversationContext>(
                informedContext.FindParticipant(source.Id));
        AiWorldConversationContext uninformedSource =
            Assert.IsType<AiWorldConversationContext>(
                uninformedContext.FindParticipant(source.Id));
        Assert.Contains(
            informedSource.RelevantKnowledge,
            knowledge => knowledge.Summary.Contains(
                "远山学院",
                StringComparison.Ordinal));
        Assert.Empty(uninformedSource.RelevantKnowledge);
        Assert.All(
            informedSource.RelevantKnowledge,
            knowledge => Assert.Equal(
                informedSpeaker.Id,
                knowledge.OwnerAiAccountId));
    }

    [Fact]
    public void PrepareGroupGenerationRequest_DoesNotExposeUnlearnedWorldName()
    {
        CharacterWorld sourceWorld = CreateWorld(
            "未公开群聊世界",
            "该世界名从未在聊天中出现。");
        AiAccount speaker = CreateAccount(
            "保守群聊成员",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("陌生群聊成员", sourceWorld.Id);

        AiGroupWorldConversationContext context =
            Assert.IsType<AiGroupWorldConversationContext>(
                CreateContextService()
                    .PrepareGroupGenerationRequest(CreateGroupRequest(
                        speaker,
                        new[] { source },
                        "第一次在群里见面。"))
                    .GroupWorldConversationContext);

        AiWorldConversationContext sourceContext =
            Assert.IsType<AiWorldConversationContext>(
                context.FindParticipant(source.Id));
        Assert.Equal(
            AiWorldAwarenessState.AssumedSharedWorld,
            sourceContext.RelationshipAwareness);
        Assert.Null(sourceContext.VisibleSubjectWorldName);
        Assert.Empty(sourceContext.RelevantKnowledge);
    }

    private AiWorldConversationContextService CreateContextService()
    {
        VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        return new AiWorldConversationContextService(
            factory,
            new AiWorldAwarenessService(factory),
            new AiWorldKnowledgeService(factory),
            new AiWorldKnowledgeCandidateExtractor());
    }

    private AiWorldKnowledgeMessageProcessor CreateProcessor()
    {
        VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        return new AiWorldKnowledgeMessageProcessor(
            factory,
            new AiWorldKnowledgeCandidateExtractor(),
            new AiWorldKnowledgeService(factory),
            new AiWorldAwarenessService(factory));
    }

    private AiMessageGenerationRequest CreateRequest(
        AiAccount speaker,
        AiAccount subject,
        string focusContent,
        PrivateMessage? replyMessage = null)
    {
        AiDialogueMessage? target = replyMessage is null
            ? null
            : ToDialogueMessage(replyMessage);
        return new AiMessageGenerationRequest
        {
            Scenario =
                AiMessageGenerationScenario.AutonomousPrivateChat,
            Speaker = speaker,
            OtherParticipants = new[] { subject },
            RelationshipTarget = subject,
            FocusContent = focusContent,
            Topic = focusContent,
            ReplyTarget = target is null
                ? AiDialogueReplyTarget.ContinueTopic()
                : AiDialogueReplyTarget.ReplyTo(target),
            RecentMessages = target is null
                ? Array.Empty<AiDialogueMessage>()
                : new[] { target }
        };
    }

    private static AiMessageGenerationRequest CreateGroupRequest(
        AiAccount speaker,
        IReadOnlyList<AiAccount> otherParticipants,
        string focusContent)
    {
        return new AiMessageGenerationRequest
        {
            Scenario = AiMessageGenerationScenario.GroupPrimaryReply,
            Speaker = speaker,
            OtherParticipants = otherParticipants,
            FocusContent = focusContent,
            Topic = focusContent,
            ReplyTarget = AiDialogueReplyTarget.ContinueTopic()
        };
    }

    private CharacterWorld CreateWorld(
        string name,
        string description)
    {
        CharacterWorldService service =
            new(_database.CreateDbContextFactory());
        Assert.Equal(
            CharacterWorldOperationStatus.Success,
            service.TryCreate(
                name,
                description,
                out CharacterWorld? world,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);
        return Assert.IsType<CharacterWorld>(world);
    }

    private AiAccount CreateAccount(
        string nickname,
        Guid characterWorldId)
    {
        AiAccountService service =
            new(_database.CreateDbContextFactory());
        Assert.True(
            service.TryCreateAiAccount(
                new AiAccountCreationData
                {
                    Nickname = nickname,
                    CharacterWorldId = characterWorldId
                },
                out AiAccount? account,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private PrivateChat CreateAiPrivateChat(
        AiAccount first,
        AiAccount second)
    {
        PrivateChatService service =
            new(_database.CreateDbContextFactory());
        Assert.True(
            service.TryGetOrCreateAiPrivateChat(
                first.Id,
                second.Id,
                out PrivateChat? chat,
                out _,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<PrivateChat>(chat);
    }

    private PrivateMessage SavePrivateMessage(
        PrivateChat chat,
        AiAccount sender,
        string content)
    {
        PrivateChatService service =
            new(_database.CreateDbContextFactory());
        Assert.True(
            service.TrySaveAiReply(
                chat,
                sender,
                content,
                out PrivateMessage? message,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<PrivateMessage>(message);
    }

    private static AiDialogueMessage ToDialogueMessage(
        PrivateMessage message) =>
        new(
            message.SenderDisplayName,
            message.Content,
            message.SenderType,
            message.SenderAiAccountId,
            message.Id,
            message.SentAt);

    public void Dispose()
    {
        _database.Dispose();
    }
}

using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证正式消息保存后的世界知识提取、监听者隔离、状态推进和幂等性。
/// </summary>
public sealed class AiWorldKnowledgeMessageProcessorTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task PrivateAiMessage_OnlyOtherParticipantLearnsAndRepeatIsIdempotent()
    {
        CharacterWorld sourceWorld = CreateWorld("私聊来源世界");
        AiAccount observer = CreateAccount(
            "私聊观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("私聊来源", sourceWorld.Id);
        AiAccount outsider = CreateAccount(
            "私聊第三方",
            CharacterWorld.DefaultWorldId);
        PrivateChat chat = CreateAiPrivateChat(observer, source);
        PrivateMessage message = SavePrivateMessage(
            chat,
            source,
            "阿拜多斯是一所受到沙漠化影响的高中。");
        AiWorldKnowledgeMessageProcessor processor = CreateProcessor();

        AiWorldKnowledgeMessageProcessingResult first =
            await processor.ProcessPrivateMessageAsync(message.Id);
        AiWorldKnowledgeMessageProcessingResult repeated =
            await processor.ProcessPrivateMessageAsync(message.Id);

        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            first.Status);
        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.AlreadyProcessed,
            repeated.Status);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        AiWorldKnowledge knowledge = Assert.Single(
            dbContext.AiWorldKnowledge.AsNoTracking());
        Assert.Equal(observer.Id, knowledge.OwnerAiAccountId);
        Assert.Equal(source.Id, knowledge.SubjectAiAccountId);
        Assert.DoesNotContain(
            dbContext.AiWorldKnowledge,
            item => item.OwnerAiAccountId == source.Id
                || item.OwnerAiAccountId == outsider.Id);
        AiWorldAwareness awareness = Assert.Single(
            dbContext.AiWorldAwareness.AsNoTracking());
        Assert.Equal(observer.Id, awareness.ObserverAiAccountId);
        Assert.Equal(source.Id, awareness.SubjectAiAccountId);
        Assert.Equal(
            AiWorldAwarenessState.AnomalyObserved,
            awareness.State);
        Assert.Equal(1, awareness.EvidenceCount);
        Assert.Single(dbContext.AiWorldKnowledgeEvidence);
    }

    [Fact]
    public async Task IndependentBackgroundSignals_AdvanceToDifferentBackground()
    {
        CharacterWorld sourceWorld = CreateWorld("背景差异世界");
        AiAccount observer = CreateAccount(
            "背景观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("背景来源", sourceWorld.Id);
        PrivateChat chat = CreateAiPrivateChat(observer, source);
        PrivateMessage firstMessage = SavePrivateMessage(
            chat,
            source,
            "我这里的学校规则和你那里不一样。");
        PrivateMessage secondMessage = SavePrivateMessage(
            chat,
            source,
            "我们这边的城市常识和你们那边完全不同。");
        AiWorldKnowledgeMessageProcessor processor = CreateProcessor();

        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            (await processor.ProcessPrivateMessageAsync(firstMessage.Id)).Status);
        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            (await processor.ProcessPrivateMessageAsync(secondMessage.Id)).Status);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        AiWorldAwareness awareness = Assert.Single(
            dbContext.AiWorldAwareness.AsNoTracking());
        Assert.Equal(
            AiWorldAwarenessState.DifferentBackgroundRecognized,
            awareness.State);
        Assert.Equal(2, awareness.EvidenceCount);
        Assert.Equal(1, awareness.DistinctConversationCount);
    }

    [Fact]
    public async Task ExplicitCrossWorldMessage_ConfirmsDirectionAndInformsListener()
    {
        CharacterWorld sourceWorld = CreateWorld("明确跨世界");
        AiAccount observer = CreateAccount(
            "确认观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("确认来源", sourceWorld.Id);
        PrivateChat chat = CreateAiPrivateChat(observer, source);
        PrivateMessage message = SavePrivateMessage(
            chat,
            source,
            "我们不在同一个世界，但现在可以跨世界通信。");

        AiWorldKnowledgeMessageProcessingResult result =
            await CreateProcessor().ProcessPrivateMessageAsync(message.Id);

        Assert.Contains(observer.Id, result.NewlyInformedAiAccountIds);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            AiParallelWorldAwarenessState.Informed,
            Assert.Single(
                dbContext.AiParallelWorldAwareness.AsNoTracking()).State);
        Assert.Equal(
            AiWorldAwarenessState.CrossWorldConfirmed,
            Assert.Single(
                dbContext.AiWorldAwareness.AsNoTracking()).State);
    }

    [Fact]
    public async Task GroupMessage_UsesSavedAudienceAndExcludesLaterMemberFromLearning()
    {
        CharacterWorld sourceWorld = CreateWorld("群聊来源世界");
        AiAccount first = CreateAccount(
            "群聊监听甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("群聊来源乙", sourceWorld.Id);
        AiAccount third = CreateAccount(
            "群聊监听丙",
            CharacterWorld.DefaultWorldId);
        AiAccount later = CreateAccount(
            "群聊后来者",
            CharacterWorld.DefaultWorldId);
        AiAccount outsider = CreateAccount(
            "群聊局外者",
            CharacterWorld.DefaultWorldId);
        GroupChat groupChat = CreateGroupChat(
            includesLocalUser: false,
            first,
            source,
            third);
        GroupMessage message = SaveGroupAiMessage(
            groupChat,
            source,
            "阿拜多斯是一所受到沙漠化影响的高中。");
        GroupChatService groupChatService =
            new(_database.CreateDbContextFactory());
        Assert.True(
            groupChatService.TryAddMember(
                groupChat,
                later.Id,
                out string addError),
            addError);

        AiWorldKnowledgeMessageProcessingResult result =
            await CreateProcessor().ProcessGroupMessageAsync(message.Id);

        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            result.Status);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(3, dbContext.GroupMessageAudience.Count());
        Assert.Equal(
            new[] { first.Id, third.Id }.OrderBy(id => id),
            dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Select(item => item.OwnerAiAccountId)
                .OrderBy(id => id));
        Assert.DoesNotContain(
            dbContext.AiWorldKnowledge,
            item => item.OwnerAiAccountId == source.Id
                || item.OwnerAiAccountId == later.Id
                || item.OwnerAiAccountId == outsider.Id);
    }

    [Fact]
    public async Task GroupMessage_AdvancesEachListenersWorldAwarenessIndependently()
    {
        CharacterWorld sourceWorld = CreateWorld("方向差异来源世界");
        AiAccount source = CreateAccount("方向差异来源", sourceWorld.Id);
        AiAccount sameWorldListener = CreateAccount(
            "同世界监听者",
            sourceWorld.Id);
        AiAccount crossWorldListener = CreateAccount(
            "跨世界监听者",
            CharacterWorld.DefaultWorldId);
        GroupChat groupChat = CreateGroupChat(
            includesLocalUser: false,
            source,
            sameWorldListener,
            crossWorldListener);
        GroupMessage message = SaveGroupAiMessage(
            groupChat,
            source,
            "远山学院是一所采用寄宿制度的学校。");

        AiWorldKnowledgeMessageProcessingResult result =
            await CreateProcessor().ProcessGroupMessageAsync(message.Id);

        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            result.Status);
        AiWorldAwarenessService awarenessService =
            new(_database.CreateDbContextFactory());
        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            awarenessService.TryGetWorldAwareness(
                sameWorldListener.Id,
                source.Id,
                out AiWorldAwarenessState sameWorldState,
                out _,
                out string sameWorldError));
        Assert.Equal(string.Empty, sameWorldError);
        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            awarenessService.TryGetWorldAwareness(
                crossWorldListener.Id,
                source.Id,
                out AiWorldAwarenessState crossWorldState,
                out _,
                out string crossWorldError));
        Assert.Equal(string.Empty, crossWorldError);
        Assert.Equal(
            AiWorldAwarenessState.AssumedSharedWorld,
            sameWorldState);
        Assert.Equal(
            AiWorldAwarenessState.AnomalyObserved,
            crossWorldState);
    }

    [Fact]
    public async Task UserGroupConfirmation_InformsMembersAndCreatesDirectionalKnowledge()
    {
        CharacterWorld otherWorld = CreateWorld("用户确认世界");
        AiAccount first = CreateAccount(
            "用户确认甲",
            CharacterWorld.DefaultWorldId);
        AiAccount second = CreateAccount("用户确认乙", otherWorld.Id);
        GroupChat groupChat = CreateGroupChat(
            includesLocalUser: true,
            first,
            second);
        GroupMessageService messageService =
            new(_database.CreateDbContextFactory());
        Assert.True(
            messageService.TrySaveUserMessage(
                groupChat,
                "你们来自不同世界，现在通过跨世界通信联系。",
                out GroupMessage? message,
                out string saveError),
            saveError);

        AiWorldKnowledgeMessageProcessingResult result =
            await CreateProcessor().ProcessGroupMessageAsync(
                Assert.IsType<GroupMessage>(message).Id);

        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(id => id),
            result.NewlyInformedAiAccountIds.OrderBy(id => id));
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(2, dbContext.AiParallelWorldAwareness.Count());
        Assert.Equal(2, dbContext.AiWorldAwareness.Count());
        Assert.All(
            dbContext.AiWorldAwareness.AsNoTracking(),
            awareness => Assert.Equal(
                AiWorldAwarenessState.CrossWorldConfirmed,
                awareness.State));
        Assert.Equal(2, dbContext.AiWorldKnowledge.Count());
        Assert.All(
            dbContext.AiWorldKnowledge.AsNoTracking(),
            knowledge => Assert.Equal(
                AiWorldKnowledgeTrustLevel.UserConfirmed,
                knowledge.TrustLevel));
    }

    [Fact]
    public async Task UserPrivateMessage_InformsOnlyCurrentAiWithoutCreatingAiRelation()
    {
        AiAccount account = CreateAccount(
            "用户私聊接收者",
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
                "确实存在平行世界，我们正在进行跨世界通信。",
                out PrivateMessage? message,
                out string messageError),
            messageError);

        AiWorldKnowledgeMessageProcessingResult result =
            await CreateProcessor().ProcessPrivateMessageAsync(
                Assert.IsType<PrivateMessage>(message).Id);

        Assert.Contains(account.Id, result.NewlyInformedAiAccountIds);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            AiParallelWorldAwarenessState.Informed,
            Assert.Single(
                dbContext.AiParallelWorldAwareness.AsNoTracking()).State);
        Assert.Empty(dbContext.AiWorldAwareness);
        Assert.Empty(dbContext.AiWorldKnowledge);
    }

    [Fact]
    public async Task GreetingMessage_DoesNotCreateKnowledgeOrAwareness()
    {
        CharacterWorld sourceWorld = CreateWorld("寒暄来源世界");
        AiAccount observer = CreateAccount(
            "寒暄观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("寒暄来源", sourceWorld.Id);
        PrivateChat chat = CreateAiPrivateChat(observer, source);
        PrivateMessage message = SavePrivateMessage(
            chat,
            source,
            "你好。");

        AiWorldKnowledgeMessageProcessingResult result =
            await CreateProcessor().ProcessPrivateMessageAsync(message.Id);

        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.NoRelevantKnowledge,
            result.Status);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Empty(dbContext.AiWorldKnowledge);
        Assert.Empty(dbContext.AiWorldAwareness);
        Assert.Empty(dbContext.AiParallelWorldAwareness);
    }

    [Fact]
    public async Task SemanticFallback_ExtractsOnceAndPersistsForListener()
    {
        CharacterWorld sourceWorld = CreateWorld("潮汐来源世界");
        AiAccount observer = CreateAccount(
            "潮汐观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("潮汐来源", sourceWorld.Id);
        PrivateChat chat = CreateAiPrivateChat(observer, source);
        PrivateMessage message = SavePrivateMessage(
            chat,
            source,
            "潮汐门的钟声只在蓝月落下时响起。");
        StaticAiWorldKnowledgeSemanticExtractor semanticExtractor = new(
            _ => new AiWorldKnowledgeSemanticExtractionResult(
                AiWorldKnowledgeSignal.UnfamiliarConcept,
                new[]
                {
                    new AiWorldKnowledgeSemanticConcept(
                        "潮汐门",
                        AiWorldKnowledgeConceptCategory.Place)
                },
                ErrorMessage: null));

        AiWorldKnowledgeMessageProcessingResult result =
            await CreateProcessor(semanticExtractor)
                .ProcessPrivateMessageAsync(message.Id);

        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            result.Status);
        Assert.Single(semanticExtractor.Requests);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        AiWorldKnowledge knowledge = Assert.Single(
            dbContext.AiWorldKnowledge.Where(item =>
                item.OwnerAiAccountId == observer.Id));
        Assert.Contains("潮汐门", knowledge.Summary);
    }

    [Fact]
    public async Task GroupSemanticFallback_ExtractsOnceAndDistributesToSavedAudience()
    {
        CharacterWorld sourceWorld = CreateWorld("星泪来源世界");
        AiAccount source = CreateAccount("星泪来源", sourceWorld.Id);
        AiAccount firstListener = CreateAccount(
            "第一位星泪观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount secondListener = CreateAccount(
            "第二位星泪观察者",
            CharacterWorld.DefaultWorldId);
        GroupChat groupChat = CreateGroupChat(
            includesLocalUser: false,
            source,
            firstListener,
            secondListener);
        GroupMessage message = SaveGroupAiMessage(
            groupChat,
            source,
            "星泪桥会在赤月升起时浮出水面。");
        StaticAiWorldKnowledgeSemanticExtractor semanticExtractor = new(
            _ => new AiWorldKnowledgeSemanticExtractionResult(
                AiWorldKnowledgeSignal.UnfamiliarConcept,
                new[]
                {
                    new AiWorldKnowledgeSemanticConcept(
                        "星泪桥",
                        AiWorldKnowledgeConceptCategory.Place)
                },
                ErrorMessage: null));

        AiWorldKnowledgeMessageProcessingResult result =
            await CreateProcessor(semanticExtractor)
                .ProcessGroupMessageAsync(message.Id);

        Assert.Equal(
            AiWorldKnowledgeMessageProcessingStatus.Success,
            result.Status);
        Assert.Single(semanticExtractor.Requests);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Guid[] owners = dbContext.AiWorldKnowledge
            .Where(item => item.Summary.Contains("星泪桥"))
            .Select(item => item.OwnerAiAccountId)
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(
            new[] { firstListener.Id, secondListener.Id }.OrderBy(id => id),
            owners);
    }

    private AiWorldKnowledgeMessageProcessor CreateProcessor(
        IAiWorldKnowledgeSemanticExtractor? semanticExtractor = null)
    {
        VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        return new AiWorldKnowledgeMessageProcessor(
            factory,
            semanticExtractor is null
                ? new AiWorldKnowledgeCandidateExtractor()
                : new AiWorldKnowledgeCandidateExtractor(semanticExtractor),
            new AiWorldKnowledgeService(factory),
            new AiWorldAwarenessService(factory));
    }

    private CharacterWorld CreateWorld(string name)
    {
        CharacterWorldService service =
            new(_database.CreateDbContextFactory());
        Assert.Equal(
            CharacterWorldOperationStatus.Success,
            service.TryCreate(
                name,
                $"{name}的测试说明。",
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

    private GroupChat CreateGroupChat(
        bool includesLocalUser,
        params AiAccount[] members)
    {
        GroupChatService service =
            new(_database.CreateDbContextFactory());
        Assert.True(
            service.TryCreateGroupChat(
                $"认知测试群-{Guid.NewGuid():N}",
                members.Select(member => member.Id),
                includesLocalUser,
                out GroupChat? groupChat,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<GroupChat>(groupChat);
    }

    private GroupMessage SaveGroupAiMessage(
        GroupChat groupChat,
        AiAccount sender,
        string content)
    {
        GroupMessageService service =
            new(_database.CreateDbContextFactory());
        Assert.True(
            service.TrySaveAiReply(
                groupChat,
                sender,
                content,
                out GroupMessage? message,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<GroupMessage>(message);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

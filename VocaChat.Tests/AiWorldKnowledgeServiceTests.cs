using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证世界知识的来源可见性、方向隔离、接收者快照和持久化。
/// </summary>
public sealed class AiWorldKnowledgeServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void PrivateKnowledge_PersistsOnlyForActualParticipant()
    {
        CharacterWorld otherWorld = CreateWorld("私聊知识世界");
        AiAccount owner = CreateAccount(
            "私聊知识甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("私聊知识乙", otherWorld.Id);
        AiAccount outsider = CreateAccount(
            "私聊知识丙",
            CharacterWorld.DefaultWorldId);
        PrivateChat privateChat = CreateAiPrivateChat(owner, source);
        PrivateMessage message = SaveAiPrivateMessage(
            privateChat,
            source,
            "阿拜多斯是一所受到沙漠化问题困扰的高中，这影响着我们的生活。");
        AiWorldKnowledgeService service = CreateService();
        AiWorldKnowledgeWriteData data = CreateWriteData(
            owner,
            otherWorld,
            source,
            " Place.Abydos.Environment ",
            " 来源账号提到阿拜多斯是一所受沙漠化困扰的高中，这影响着他们的生活。 ");

        AiWorldKnowledgeOperationStatus createStatus =
            service.TryCreateKnowledge(
                data,
                message.Id,
                sourceGroupMessageId: null,
                "来源消息明确说明学校性质、沙漠化问题及其生活影响。",
                out AiWorldKnowledge? created,
                out string createError);

        Assert.Equal(AiWorldKnowledgeOperationStatus.Success, createStatus);
        Assert.NotNull(created);
        Assert.Equal("place.abydos.environment", created.KnowledgeKey);
        Assert.Equal(string.Empty, createError);

        AiWorldKnowledgeService restarted = CreateService();
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            restarted.TryGetActiveKnowledge(
                owner.Id,
                otherWorld.Id,
                source.Id,
                10,
                out IReadOnlyList<AiWorldKnowledge> reloaded,
                out string queryError));
        Assert.Equal(string.Empty, queryError);
        Assert.Equal(created.Id, Assert.Single(reloaded).Id);

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            restarted.TryGetActiveKnowledge(
                outsider.Id,
                otherWorld.Id,
                source.Id,
                10,
                out IReadOnlyList<AiWorldKnowledge> outsiderKnowledge,
                out _));
        Assert.Empty(outsiderKnowledge);

        AiWorldKnowledgeWriteData outsiderData = data with
        {
            OwnerAiAccountId = outsider.Id
        };
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.SourceNotVisible,
            restarted.TryCreateKnowledge(
                outsiderData,
                message.Id,
                sourceGroupMessageId: null,
                "第三方不应获得这条私聊知识。",
                out _,
                out _));

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        AiWorldKnowledgeEvidence evidence = Assert.Single(
            dbContext.AiWorldKnowledgeEvidence.AsNoTracking());
        Assert.Equal(created.Id, evidence.AiWorldKnowledgeId);
        Assert.Equal(source.Id, evidence.SourceAiAccountId);
        Assert.Equal(message.Id, evidence.SourcePrivateMessageId);
        Assert.Null(evidence.SourceGroupMessageId);
    }

    [Fact]
    public void DuplicateSource_IsIdempotentAndNewSourceAddsEvidence()
    {
        CharacterWorld otherWorld = CreateWorld("多来源世界");
        AiAccount owner = CreateAccount(
            "多来源甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("多来源乙", otherWorld.Id);
        PrivateChat privateChat = CreateAiPrivateChat(owner, source);
        PrivateMessage firstMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "我们那里把那座学校称为阿拜多斯。");
        PrivateMessage secondMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "阿拜多斯确实是一所高中。");
        AiWorldKnowledgeService service = CreateService();
        AiWorldKnowledgeWriteData data = CreateWriteData(
            owner,
            otherWorld,
            source,
            "place.abydos.kind",
            "来源账号称阿拜多斯是一所高中。");

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                data,
                firstMessage.Id,
                sourceGroupMessageId: null,
                "来源账号第一次介绍阿拜多斯。",
                out AiWorldKnowledge? first,
                out _));
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.AlreadyExists,
            service.TryCreateKnowledge(
                data,
                firstMessage.Id,
                sourceGroupMessageId: null,
                "重复处理同一条来源。",
                out AiWorldKnowledge? repeated,
                out _));
        Assert.Equal(first!.Id, repeated!.Id);
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.EvidenceAdded,
            service.TryCreateKnowledge(
                data,
                secondMessage.Id,
                sourceGroupMessageId: null,
                "第二条消息再次说明阿拜多斯是高中。",
                out AiWorldKnowledge? supported,
                out _));
        Assert.Equal(first.Id, supported!.Id);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(1, dbContext.AiWorldKnowledge.Count());
        Assert.Equal(2, dbContext.AiWorldKnowledgeEvidence.Count());
    }

    [Fact]
    public void EquivalentKnowledgeFromIndependentSources_IsMergedAndCorroborated()
    {
        CharacterWorld otherWorld = CreateWorld("归并知识世界");
        AiAccount owner = CreateAccount(
            "归并知识甲",
            CharacterWorld.DefaultWorldId);
        AiAccount firstSource = CreateAccount("归并知识乙", otherWorld.Id);
        AiAccount secondSource = CreateAccount("归并知识丙", otherWorld.Id);
        PrivateMessage firstMessage = SaveAiPrivateMessage(
            CreateAiPrivateChat(owner, firstSource),
            firstSource,
            "阿拜多斯是一所高中。");
        PrivateMessage secondMessage = SaveAiPrivateMessage(
            CreateAiPrivateChat(owner, secondSource),
            secondSource,
            "阿拜多斯是个学校。");
        AiWorldKnowledgeService service = CreateService();
        AiWorldKnowledgeWriteData firstData = CreateWriteData(
            owner,
            otherWorld,
            firstSource,
            "place.abydos.kind",
            "阿拜多斯是一所高中。");
        AiWorldKnowledgeWriteData secondData = firstData with
        {
            SubjectAiAccountId = firstSource.Id,
            KnowledgeKey = "legacy.abydos.school",
            Summary = "阿拜多斯是个学校。"
        };

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                firstData,
                firstMessage.Id,
                sourceGroupMessageId: null,
                firstData.Summary,
                out AiWorldKnowledge? first,
                out _));
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.EvidenceAdded,
            service.TryCreateKnowledge(
                secondData,
                secondMessage.Id,
                sourceGroupMessageId: null,
                secondData.Summary,
                out AiWorldKnowledge? merged,
                out _));

        Assert.Equal(first!.Id, merged!.Id);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        AiWorldKnowledge stored = Assert.Single(
            dbContext.AiWorldKnowledge.AsNoTracking());
        Assert.Equal(
            AiWorldKnowledgeTrustLevel.Corroborated,
            stored.TrustLevel);
        Assert.Equal(2, dbContext.AiWorldKnowledgeEvidence.Count());
    }

    [Fact]
    public void SameNamedConceptsInDifferentWorlds_AreNotMerged()
    {
        CharacterWorld firstWorld = CreateWorld("同名概念世界甲");
        CharacterWorld secondWorld = CreateWorld("同名概念世界乙");
        AiAccount owner = CreateAccount(
            "同名概念观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount firstSource = CreateAccount(
            "同名概念讲述者甲",
            firstWorld.Id);
        AiAccount secondSource = CreateAccount(
            "同名概念讲述者乙",
            secondWorld.Id);
        PrivateMessage firstMessage = SaveAiPrivateMessage(
            CreateAiPrivateChat(owner, firstSource),
            firstSource,
            "我们那边有一座叫中央城的城市。");
        PrivateMessage secondMessage = SaveAiPrivateMessage(
            CreateAiPrivateChat(owner, secondSource),
            secondSource,
            "我们那边也有一座叫中央城的城市。");
        AiWorldKnowledgeService service = CreateService();

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                CreateWriteData(
                    owner,
                    firstWorld,
                    firstSource,
                    "place.central-city",
                    "讲述者所在世界有一座名为中央城的城市。"),
                firstMessage.Id,
                sourceGroupMessageId: null,
                "第一个世界对中央城的说明。",
                out AiWorldKnowledge? first,
                out _));
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                CreateWriteData(
                    owner,
                    secondWorld,
                    secondSource,
                    "place.central-city",
                    "讲述者所在世界有一座名为中央城的城市。"),
                secondMessage.Id,
                sourceGroupMessageId: null,
                "第二个世界对中央城的说明。",
                out AiWorldKnowledge? second,
                out _));

        Assert.NotEqual(first!.Id, second!.Id);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(2, dbContext.AiWorldKnowledge.Count());
        Assert.Equal(
            2,
            dbContext.AiWorldKnowledge
                .Select(item => item.SubjectCharacterWorldId)
                .Distinct()
                .Count());
    }

    [Fact]
    public void ConstantObjectiveConflict_IsPreservedForUserDecision()
    {
        CharacterWorld otherWorld = CreateWorld("冲突知识世界");
        AiAccount owner = CreateAccount(
            "冲突知识甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("冲突知识乙", otherWorld.Id);
        PrivateChat privateChat = CreateAiPrivateChat(owner, source);
        PrivateMessage firstMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "阿拜多斯是一所高中。");
        PrivateMessage secondMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "阿拜多斯并不是学校，而是一座军事基地。");
        AiWorldKnowledgeService service = CreateService();
        AiWorldKnowledgeWriteData firstData = CreateWriteData(
            owner,
            otherWorld,
            source,
            "place.abydos.kind",
            "阿拜多斯是一所高中。") with
        {
            FactNature =
                AiWorldKnowledgeFactNature.ObjectiveStatement,
            Mutability = AiWorldKnowledgeMutability.Constant,
            IsUserLocked = true
        };
        AiWorldKnowledgeWriteData conflictingData = firstData with
        {
            Summary = "阿拜多斯并不是学校，而是一座军事基地。"
        };

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                firstData,
                firstMessage.Id,
                sourceGroupMessageId: null,
                firstData.Summary,
                out AiWorldKnowledge? active,
                out _));
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.ConflictCandidateCreated,
            service.TryCreateKnowledge(
                conflictingData,
                secondMessage.Id,
                sourceGroupMessageId: null,
                conflictingData.Summary,
                out AiWorldKnowledge? conflict,
                out _));

        Assert.NotEqual(active!.Id, conflict!.Id);
        Assert.Equal(
            AiWorldKnowledgeStatus.ConflictCandidate,
            conflict.Status);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Single(dbContext.AiWorldKnowledge.Where(item =>
            item.Status == AiWorldKnowledgeStatus.Active));
        Assert.Single(dbContext.AiWorldKnowledge.Where(item =>
            item.Status
                == AiWorldKnowledgeStatus.ConflictCandidate));
        AiWorldKnowledge storedActive = dbContext.AiWorldKnowledge
            .AsNoTracking()
            .Single(item => item.Id == active.Id);
        Assert.True(storedActive.IsUserLocked);
        Assert.Equal(firstData.Summary, storedActive.Summary);
    }

    [Fact]
    public void SubjectiveViewChange_SupersedesOldVersion()
    {
        CharacterWorld otherWorld = CreateWorld("态度变化世界");
        AiAccount owner = CreateAccount(
            "态度变化甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("态度变化乙", otherWorld.Id);
        PrivateChat privateChat = CreateAiPrivateChat(owner, source);
        PrivateMessage firstMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "我以前很喜欢那座城市。");
        PrivateMessage secondMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "现在我对那座城市有些失望。");
        AiWorldKnowledgeService service = CreateService();
        AiWorldKnowledgeWriteData firstData = CreateWriteData(
            owner,
            otherWorld,
            source,
            "opinion.city",
            "来源账号以前很喜欢那座城市。") with
        {
            FactNature = AiWorldKnowledgeFactNature.SubjectiveView
        };
        AiWorldKnowledgeWriteData secondData = firstData with
        {
            Summary = "来源账号现在对那座城市有些失望。"
        };

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                firstData,
                firstMessage.Id,
                sourceGroupMessageId: null,
                firstData.Summary,
                out AiWorldKnowledge? first,
                out _));
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.KnowledgeSuperseded,
            service.TryCreateKnowledge(
                secondData,
                secondMessage.Id,
                sourceGroupMessageId: null,
                secondData.Summary,
                out AiWorldKnowledge? current,
                out _));

        Assert.NotEqual(first!.Id, current!.Id);
        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            AiWorldKnowledgeStatus.Superseded,
            dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Single(item => item.Id == first.Id)
                .Status);
        Assert.Equal(
            AiWorldKnowledgeStatus.Active,
            dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Single(item => item.Id == current.Id)
                .Status);
    }

    [Fact]
    public void UserCanConfirmConflictAndReadItsSourceMessage()
    {
        CharacterWorld otherWorld = CreateWorld("用户确认世界");
        AiAccount owner = CreateAccount(
            "用户确认甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("用户确认乙", otherWorld.Id);
        PrivateChat privateChat = CreateAiPrivateChat(owner, source);
        PrivateMessage firstMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "那座城常年封闭。");
        PrivateMessage conflictMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "那座城从来没有封闭。");
        AiWorldKnowledgeService service = CreateService();
        AiWorldKnowledgeWriteData firstData = CreateWriteData(
            owner,
            otherWorld,
            source,
            "city.access",
            "那座城常年封闭。") with
        {
            FactNature =
                AiWorldKnowledgeFactNature.ObjectiveStatement,
            Mutability = AiWorldKnowledgeMutability.Constant
        };
        AiWorldKnowledgeWriteData conflictData = firstData with
        {
            Summary = "那座城从来没有封闭。"
        };
        service.TryCreateKnowledge(
            firstData,
            firstMessage.Id,
            null,
            firstData.Summary,
            out AiWorldKnowledge? original,
            out _);
        service.TryCreateKnowledge(
            conflictData,
            conflictMessage.Id,
            null,
            conflictData.Summary,
            out AiWorldKnowledge? conflict,
            out _);

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryUpdateByUser(
                owner.Id,
                conflict!.Id,
                new AiWorldKnowledgeUserUpdateData(
                    "用户确认那座城目前没有封闭。",
                    AiWorldKnowledgeFactNature.ObjectiveStatement,
                    AiWorldKnowledgeMutability.Constant,
                    Salience: 90,
                    IsUserLocked: true,
                    IsConfirmed: true),
                out AiWorldKnowledge? confirmed,
                out string updateError));
        Assert.Equal(string.Empty, updateError);
        Assert.Equal(AiWorldKnowledgeStatus.Active, confirmed!.Status);
        Assert.Equal(
            AiWorldKnowledgeTrustLevel.UserConfirmed,
            confirmed.TrustLevel);
        Assert.True(confirmed.IsUserLocked);

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryGetEvidenceDetails(
                owner.Id,
                confirmed.Id,
                out IReadOnlyList<AiWorldKnowledgeEvidenceDetails> evidence,
                out string evidenceError));
        Assert.Equal(string.Empty, evidenceError);
        AiWorldKnowledgeEvidenceDetails sourceDetails =
            Assert.Single(evidence);
        Assert.Equal(conflictMessage.Content, sourceDetails.MessageContent);
        Assert.Equal(source.Nickname, sourceDetails.SourceDisplayName);
        Assert.Equal("PrivateChat", sourceDetails.ConversationKind);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Equal(
            AiWorldKnowledgeStatus.Superseded,
            dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Single(item => item.Id == original!.Id)
                .Status);
    }

    [Fact]
    public void GroupAudienceSnapshot_ExcludesLaterMemberAndProtectsKnowledge()
    {
        CharacterWorld otherWorld = CreateWorld("群聊知识世界");
        AiAccount first = CreateAccount(
            "群聊知识甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("群聊知识乙", otherWorld.Id);
        AiAccount third = CreateAccount(
            "群聊知识丙",
            CharacterWorld.DefaultWorldId);
        AiAccount laterMember = CreateAccount(
            "群聊知识后来者",
            CharacterWorld.DefaultWorldId);
        GroupChat groupChat = CreateGroupChat(first, source, third);
        GroupMessage message = SaveGroupMessage(
            groupChat,
            source,
            "我那边的学校正在面对持续的沙漠化问题。");
        AiWorldKnowledgeService service = CreateService();

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryGetGroupMessageAudience(
                message.Id,
                out IReadOnlyList<GroupMessageAudience> firstSnapshot,
                out string snapshotError));
        Assert.Equal(string.Empty, snapshotError);
        Assert.Equal(
            new[] { first.Id, source.Id, third.Id }.OrderBy(id => id),
            firstSnapshot.Select(item => item.AiAccountId));

        GroupChatService groupChatService =
            new(_database.CreateDbContextFactory());
        Assert.True(
            groupChatService.TryAddMember(
                groupChat,
                laterMember.Id,
                out string addError),
            addError);

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.AlreadyExists,
            service.TryRecordGroupMessageAudience(
                message.Id,
                out IReadOnlyList<GroupMessageAudience> secondSnapshot,
                out _));
        Assert.Equal(3, secondSnapshot.Count);
        Assert.DoesNotContain(
            secondSnapshot,
            item => item.AiAccountId == laterMember.Id);

        AiWorldKnowledgeWriteData firstData = CreateWriteData(
            first,
            otherWorld,
            source,
            "environment.desertification",
            "来源账号提到其学校正面对持续的沙漠化问题。");
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                firstData,
                sourcePrivateMessageId: null,
                message.Id,
                "群消息说明来源账号所在地存在沙漠化问题。",
                out _,
                out _));

        AiWorldKnowledgeWriteData laterData = firstData with
        {
            OwnerAiAccountId = laterMember.Id
        };
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.SourceNotVisible,
            service.TryCreateKnowledge(
                laterData,
                sourcePrivateMessageId: null,
                message.Id,
                "后来入群者不应获得历史群消息知识。",
                out _,
                out _));
    }

    [Fact]
    public void InvalidScopeAndSelfAuthoredSource_AreRejected()
    {
        CharacterWorld otherWorld = CreateWorld("无效知识世界");
        AiAccount owner = CreateAccount(
            "无效知识甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("无效知识乙", otherWorld.Id);
        PrivateChat privateChat = CreateAiPrivateChat(owner, source);
        PrivateMessage sourceMessage = SaveAiPrivateMessage(
            privateChat,
            source,
            "这是我自己发送的世界说明。");
        AiWorldKnowledgeService service = CreateService();

        AiWorldKnowledgeWriteData ownWorldData = CreateWriteData(
            owner,
            new CharacterWorldReference(
                CharacterWorld.DefaultWorldId),
            subject: null,
            "local.fact",
            "自身世界事实不应保存到其他世界知识。");
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.InvalidSubject,
            service.TryCreateKnowledge(
                ownWorldData,
                sourceMessage.Id,
                sourceGroupMessageId: null,
                "无效世界作用域。",
                out _,
                out _));

        AiWorldKnowledgeWriteData sourceAsOwner = CreateWriteData(
            source,
            new CharacterWorldReference(
                CharacterWorld.DefaultWorldId),
            owner,
            "self.authored",
            "发送者不能从自己的消息学习。");
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.SelfAuthoredSource,
            service.TryCreateKnowledge(
                sourceAsOwner,
                sourceMessage.Id,
                sourceGroupMessageId: null,
                "发送者自己的消息。",
                out _,
                out _));
    }

    [Fact]
    public void UserLockAndArchive_PreserveStoredEvidence()
    {
        CharacterWorld otherWorld = CreateWorld("归档知识世界");
        AiAccount owner = CreateAccount(
            "归档知识甲",
            CharacterWorld.DefaultWorldId);
        AiAccount source = CreateAccount("归档知识乙", otherWorld.Id);
        PrivateChat privateChat = CreateAiPrivateChat(owner, source);
        PrivateMessage message = SaveAiPrivateMessage(
            privateChat,
            source,
            "这是一条以后需要归档的世界知识。");
        AiWorldKnowledgeService service = CreateService();

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                CreateWriteData(
                    owner,
                    otherWorld,
                    source,
                    "archive.example",
                    "来源账号提供了一条可归档的世界知识。"),
                message.Id,
                sourceGroupMessageId: null,
                "来源消息用于验证归档保留证据。",
                out AiWorldKnowledge? knowledge,
                out _));
        Assert.NotNull(knowledge);

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TrySetUserLock(
                knowledge.Id,
                isUserLocked: true,
                out AiWorldKnowledge? locked,
                out _));
        Assert.True(locked!.IsUserLocked);

        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryArchiveByUser(
                knowledge.Id,
                out AiWorldKnowledge? archived,
                out _));
        Assert.Equal(AiWorldKnowledgeStatus.Archived, archived!.Status);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Single(dbContext.AiWorldKnowledgeEvidence);
        Assert.Equal(
            AiWorldKnowledgeStatus.Archived,
            dbContext.AiWorldKnowledge
                .AsNoTracking()
                .Single(item => item.Id == knowledge.Id)
                .Status);
    }

    private AiWorldKnowledgeService CreateService() =>
        new(_database.CreateDbContextFactory());

    private CharacterWorld CreateWorld(string name)
    {
        CharacterWorldService service =
            new(_database.CreateDbContextFactory());
        Assert.Equal(
            CharacterWorldOperationStatus.Success,
            service.TryCreate(
                name,
                $"{name}的用户定义说明。",
                out CharacterWorld? world,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);
        return Assert.IsType<CharacterWorld>(world);
    }

    private AiAccount CreateAccount(string nickname, Guid characterWorldId)
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
                out PrivateChat? privateChat,
                out _,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<PrivateChat>(privateChat);
    }

    private PrivateMessage SaveAiPrivateMessage(
        PrivateChat privateChat,
        AiAccount sender,
        string content)
    {
        PrivateChatService service =
            new(_database.CreateDbContextFactory());
        Assert.True(
            service.TrySaveAiReply(
                privateChat,
                sender,
                content,
                out PrivateMessage? message,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<PrivateMessage>(message);
    }

    private GroupChat CreateGroupChat(params AiAccount[] members)
    {
        GroupChatService service =
            new(_database.CreateDbContextFactory());
        Assert.True(
            service.TryCreateGroupChat(
                "世界知识测试群",
                members.Select(member => member.Id),
                includesLocalUser: false,
                out GroupChat? groupChat,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<GroupChat>(groupChat);
    }

    private GroupMessage SaveGroupMessage(
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

    private static AiWorldKnowledgeWriteData CreateWriteData(
        AiAccount owner,
        CharacterWorld world,
        AiAccount? subject,
        string knowledgeKey,
        string summary)
    {
        return CreateWriteData(
            owner,
            new CharacterWorldReference(world.Id),
            subject,
            knowledgeKey,
            summary);
    }

    private static AiWorldKnowledgeWriteData CreateWriteData(
        AiAccount owner,
        CharacterWorldReference world,
        AiAccount? subject,
        string knowledgeKey,
        string summary)
    {
        return new AiWorldKnowledgeWriteData(
            owner.Id,
            world.Id,
            subject?.Id,
            knowledgeKey,
            summary,
            AiWorldKnowledgeFactNature.Hearsay,
            AiWorldKnowledgeMutability.Changeable,
            AiWorldKnowledgeTrustLevel.DirectStatement,
            Salience: 80,
            IsUserLocked: false);
    }

    private readonly record struct CharacterWorldReference(Guid Id);

    public void Dispose()
    {
        _database.Dispose();
    }
}

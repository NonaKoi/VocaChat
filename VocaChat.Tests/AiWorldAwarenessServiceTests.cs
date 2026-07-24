using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证账号级平行世界元认知和方向性世界认知的默认值、来源与持久化。
/// </summary>
public sealed class AiWorldAwarenessServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void MissingRows_ReturnBusinessDefaultsWithoutBackfill()
    {
        AiAccount first = CreateAccount(
            "默认认知甲",
            CharacterWorld.DefaultWorldId);
        AiAccount second = CreateAccount(
            "默认认知乙",
            CreateWorld("默认认知异世界").Id);
        AiWorldAwarenessService service = CreateService();

        AiWorldAwarenessOperationStatus parallelStatus =
            service.TryGetParallelWorldAwareness(
                first.Id,
                out AiParallelWorldAwarenessState parallelState,
                out AiParallelWorldAwareness? parallelRecord,
                out string parallelError);
        AiWorldAwarenessOperationStatus relationshipStatus =
            service.TryGetWorldAwareness(
                first.Id,
                second.Id,
                out AiWorldAwarenessState relationshipState,
                out AiWorldAwareness? relationshipRecord,
                out string relationshipError);

        Assert.Equal(AiWorldAwarenessOperationStatus.Success, parallelStatus);
        Assert.Equal(
            AiParallelWorldAwarenessState.Unaware,
            parallelState);
        Assert.Null(parallelRecord);
        Assert.Equal(string.Empty, parallelError);
        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            relationshipStatus);
        Assert.Equal(
            AiWorldAwarenessState.AssumedSharedWorld,
            relationshipState);
        Assert.Null(relationshipRecord);
        Assert.Equal(string.Empty, relationshipError);

        using VocaChatDbContext dbContext =
            _database.CreateDbContextFactory().CreateDbContext();
        Assert.Empty(dbContext.AiParallelWorldAwareness);
        Assert.Empty(dbContext.AiWorldAwareness);
    }

    [Fact]
    public void UserSettings_PersistAndRemainDirectional()
    {
        AiAccount first = CreateAccount(
            "用户认知甲",
            CharacterWorld.DefaultWorldId);
        AiAccount second = CreateAccount(
            "用户认知乙",
            CreateWorld("用户认知异世界").Id);
        AiWorldAwarenessService service = CreateService();

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            service.TrySetParallelWorldAwarenessByUser(
                first.Id,
                AiParallelWorldAwarenessState.Accepted,
                isUserLocked: true,
                out AiParallelWorldAwareness? parallel,
                out string parallelError));
        Assert.NotNull(parallel);
        Assert.True(parallel.IsUserLocked);
        Assert.Equal(string.Empty, parallelError);

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            service.TrySetWorldAwarenessByUser(
                first.Id,
                second.Id,
                AiWorldAwarenessState.CrossWorldConfirmed,
                isUserLocked: true,
                out AiWorldAwareness? direction,
                out string directionError));
        Assert.NotNull(direction);
        Assert.Equal(second.CharacterWorldId, direction.SubjectCharacterWorldId);
        Assert.True(direction.IsUserLocked);
        Assert.Equal(string.Empty, directionError);

        AiWorldAwarenessService restarted = CreateService();
        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            restarted.TryGetParallelWorldAwareness(
                first.Id,
                out AiParallelWorldAwarenessState reloadedParallel,
                out AiParallelWorldAwareness? reloadedParallelRecord,
                out _));
        Assert.Equal(
            AiParallelWorldAwarenessState.Accepted,
            reloadedParallel);
        Assert.NotNull(reloadedParallelRecord);

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            restarted.TryGetWorldAwareness(
                first.Id,
                second.Id,
                out AiWorldAwarenessState firstDirection,
                out AiWorldAwareness? firstDirectionRecord,
                out _));
        Assert.Equal(
            AiWorldAwarenessState.CrossWorldConfirmed,
            firstDirection);
        Assert.NotNull(firstDirectionRecord);

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            restarted.TryGetWorldAwareness(
                second.Id,
                first.Id,
                out AiWorldAwarenessState reverseDirection,
                out AiWorldAwareness? reverseRecord,
                out _));
        Assert.Equal(
            AiWorldAwarenessState.AssumedSharedWorld,
            reverseDirection);
        Assert.Null(reverseRecord);
    }

    [Fact]
    public void VisiblePrivateMessage_CanAdvanceObserverButNotSender()
    {
        AiAccount observer = CreateAccount(
            "消息认知甲",
            CharacterWorld.DefaultWorldId);
        AiAccount subject = CreateAccount(
            "消息认知乙",
            CreateWorld("消息认知异世界").Id);
        PrivateChat privateChat = CreateAiPrivateChat(observer, subject);
        PrivateMessage sourceMessage = SaveAiPrivateMessage(
            privateChat,
            subject,
            "我生活的地方和你描述的环境似乎很不一样。");
        AiWorldAwarenessService service = CreateService();

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            service.TryRecordParallelWorldAwareness(
                observer.Id,
                AiParallelWorldAwarenessState.Informed,
                sourceMessage.Id,
                sourceGroupMessageId: null,
                out AiParallelWorldAwareness? parallel,
                out string parallelError));
        Assert.NotNull(parallel);
        Assert.Equal(sourceMessage.Id, parallel.LastSourcePrivateMessageId);
        Assert.Equal(string.Empty, parallelError);

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            service.TryRecordWorldAwareness(
                observer.Id,
                subject.Id,
                AiWorldAwarenessState.AnomalyObserved,
                evidenceCount: 1,
                distinctConversationCount: 1,
                sourceMessage.Id,
                sourceGroupMessageId: null,
                out AiWorldAwareness? awareness,
                out string awarenessError));
        Assert.NotNull(awareness);
        Assert.Equal(1, awareness.EvidenceCount);
        Assert.Equal(sourceMessage.Id, awareness.LastSourcePrivateMessageId);
        Assert.Equal(string.Empty, awarenessError);

        Assert.Equal(
            AiWorldAwarenessOperationStatus.SelfAuthoredSource,
            service.TryRecordParallelWorldAwareness(
                subject.Id,
                AiParallelWorldAwarenessState.Informed,
                sourceMessage.Id,
                sourceGroupMessageId: null,
                out _,
                out _));

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            service.TrySetParallelWorldAwarenessByUser(
                observer.Id,
                AiParallelWorldAwarenessState.Accepted,
                isUserLocked: true,
                out _,
                out _));
        Assert.Equal(
            AiWorldAwarenessOperationStatus.UserLocked,
            service.TryRecordParallelWorldAwareness(
                observer.Id,
                AiParallelWorldAwarenessState.Accepted,
                sourceMessage.Id,
                sourceGroupMessageId: null,
                out _,
                out _));
    }

    [Fact]
    public void Familiarity_IsDerivedFromKnowledgeTopicsAndConversations()
    {
        CharacterWorld otherWorld = CreateWorld("熟悉度异世界");
        AiAccount observer = CreateAccount(
            "熟悉度观察者",
            CharacterWorld.DefaultWorldId);
        AiAccount subject = CreateAccount(
            "熟悉度讲述者",
            otherWorld.Id);
        AiAccount groupMember = CreateAccount(
            "熟悉度群成员",
            CharacterWorld.DefaultWorldId);
        PrivateChat privateChat = CreateAiPrivateChat(observer, subject);
        PrivateMessage privateMessage = SaveAiPrivateMessage(
            privateChat,
            subject,
            "我们那边的学校位于沙漠边缘，学生会负责维持日常运转。");
        GroupChat groupChat = CreateGroupChat(
            observer,
            subject,
            groupMember);
        GroupMessage groupMessage = SaveGroupMessage(
            groupChat,
            subject,
            "那座城市依靠一条地下水路维持供水。");
        AiWorldKnowledgeService knowledgeService =
            new(_database.CreateDbContextFactory());

        SaveKnowledge(
            knowledgeService,
            observer,
            subject,
            otherWorld,
            "school.location",
            "讲述者所在世界的学校位于沙漠边缘。",
            privateMessage.Id,
            sourceGroupMessageId: null);

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            CreateService().TryGetFamiliarity(
                observer.Id,
                subject.Id,
                out AiWorldFamiliarity firstImpression,
                out string firstError));
        Assert.Equal(string.Empty, firstError);
        Assert.Equal(
            AiWorldFamiliarityLevel.FirstImpression,
            firstImpression.Level);

        SaveKnowledge(
            knowledgeService,
            observer,
            subject,
            otherWorld,
            "school.governance",
            "讲述者所在世界的学生会负责学校的日常运转。",
            privateMessage.Id,
            sourceGroupMessageId: null);
        SaveKnowledge(
            knowledgeService,
            observer,
            subject,
            otherWorld,
            "city.water",
            "讲述者所在世界的城市依靠地下水路维持供水。",
            sourcePrivateMessageId: null,
            groupMessage.Id);

        Assert.Equal(
            AiWorldAwarenessOperationStatus.Success,
            CreateService().TryGetFamiliarity(
                observer.Id,
                subject.Id,
                out AiWorldFamiliarity learning,
                out string learningError));
        Assert.Equal(string.Empty, learningError);
        Assert.Equal(AiWorldFamiliarityLevel.Learning, learning.Level);
        Assert.Equal(3, learning.ActiveKnowledgeCount);
        Assert.Equal(3, learning.DistinctTopicCount);
        Assert.Equal(3, learning.EvidenceCount);
        Assert.Equal(2, learning.DistinctConversationCount);
    }

    private AiWorldAwarenessService CreateService() =>
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
                $"熟悉度群聊-{Guid.NewGuid():N}",
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

    private static void SaveKnowledge(
        AiWorldKnowledgeService service,
        AiAccount observer,
        AiAccount subject,
        CharacterWorld subjectWorld,
        string knowledgeKey,
        string summary,
        Guid? sourcePrivateMessageId,
        Guid? sourceGroupMessageId)
    {
        AiWorldKnowledgeWriteData data = new(
            observer.Id,
            subjectWorld.Id,
            subject.Id,
            knowledgeKey,
            summary,
            AiWorldKnowledgeFactNature.ObjectiveStatement,
            AiWorldKnowledgeMutability.Changeable,
            AiWorldKnowledgeTrustLevel.DirectStatement,
            Salience: 50,
            IsUserLocked: false);
        Assert.Equal(
            AiWorldKnowledgeOperationStatus.Success,
            service.TryCreateKnowledge(
                data,
                sourcePrivateMessageId,
                sourceGroupMessageId,
                summary,
                out _,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

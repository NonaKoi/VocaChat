using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证跨世界认知 Migration 在保留既有账号、群聊和消息的同时新增持久化结构。
/// </summary>
public sealed class CrossWorldKnowledgeMigrationTests
{
    private const string PreviousMigration =
        "20260723104758_AddScopedSelfMemoryFacts";
    private const string CrossWorldKnowledgeMigration =
        "20260724002047_AddCrossWorldAwarenessAndKnowledge";

    [Fact]
    public void Migration_PreservesExistingDataAndAddsOnlyEmptyNewStores()
    {
        using SqliteTestDatabase database =
            new(applyMigrations: false);
        VocaChatDbContextFactory factory =
            database.CreateDbContextFactory();

        using (VocaChatDbContext dbContext = factory.CreateDbContext())
        {
            dbContext.GetService<IMigrator>().Migrate(PreviousMigration);
        }

        AiAccount first = CreateAccount(factory, "迁移保留甲");
        AiAccount second = CreateAccount(factory, "迁移保留乙");
        GroupChat groupChat = CreateGroupChat(factory, first, second);
        GroupMessage message = SaveGroupMessage(
            factory,
            groupChat,
            first,
            "迁移前已经存在的群消息");

        using (VocaChatDbContext dbContext = factory.CreateDbContext())
        {
            dbContext.GetService<IMigrator>().Migrate();
        }

        using VocaChatDbContext reloaded = factory.CreateDbContext();
        Assert.Equal(2, reloaded.AiAccounts.Count());
        Assert.Single(reloaded.GroupChats);
        Assert.Equal(
            message.Content,
            Assert.Single(reloaded.GroupMessages).Content);
        Assert.Empty(reloaded.AiParallelWorldAwareness);
        Assert.Empty(reloaded.AiWorldAwareness);
        Assert.Empty(reloaded.AiWorldKnowledge);
        Assert.Empty(reloaded.AiWorldKnowledgeEvidence);
        Assert.Empty(reloaded.GroupMessageAudience);
    }

    [Fact]
    public void ManagementMigration_PreservesExistingWorldKnowledge()
    {
        using SqliteTestDatabase database =
            new(applyMigrations: false);
        VocaChatDbContextFactory factory =
            database.CreateDbContextFactory();

        using (VocaChatDbContext dbContext = factory.CreateDbContext())
        {
            dbContext.GetService<IMigrator>()
                .Migrate(CrossWorldKnowledgeMigration);
        }

        CharacterWorldService worldService = new(factory);
        Assert.Equal(
            CharacterWorldOperationStatus.Success,
            worldService.TryCreate(
                "迁移保留知识世界",
                "用于验证管理迁移不会丢失已有世界知识。",
                out CharacterWorld? world,
                out string worldError));
        Assert.Equal(string.Empty, worldError);
        CharacterWorld subjectWorld =
            Assert.IsType<CharacterWorld>(world);
        AiAccount owner = CreateAccount(factory, "迁移知识持有者");
        AiAccount subject = CreateAccount(
            factory,
            "迁移知识讲述者",
            subjectWorld.Id);
        AiWorldKnowledge knowledge = new(
            owner.Id,
            subjectWorld.Id,
            subject.Id,
            "migration.preserved",
            "这条世界知识在管理迁移前已经存在。",
            AiWorldKnowledgeFactNature.ObjectiveStatement,
            AiWorldKnowledgeMutability.Constant,
            AiWorldKnowledgeTrustLevel.DirectStatement,
            salience: 70,
            isUserLocked: false,
            DateTime.UtcNow);

        using (VocaChatDbContext dbContext = factory.CreateDbContext())
        {
            dbContext.AiWorldKnowledge.Add(knowledge);
            dbContext.SaveChanges();
            dbContext.GetService<IMigrator>().Migrate();
        }

        using VocaChatDbContext reloaded = factory.CreateDbContext();
        AiWorldKnowledge stored = Assert.Single(
            reloaded.AiWorldKnowledge.AsNoTracking());
        Assert.Equal(knowledge.Id, stored.Id);
        Assert.Equal(knowledge.Summary, stored.Summary);
        Assert.Equal(AiWorldKnowledgeStatus.Active, stored.Status);
    }

    private static AiAccount CreateAccount(
        VocaChatDbContextFactory factory,
        string nickname)
    {
        return CreateAccount(
            factory,
            nickname,
            CharacterWorld.DefaultWorldId);
    }

    private static AiAccount CreateAccount(
        VocaChatDbContextFactory factory,
        string nickname,
        Guid characterWorldId)
    {
        AiAccountService service = new(factory);
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

    private static GroupChat CreateGroupChat(
        VocaChatDbContextFactory factory,
        params AiAccount[] members)
    {
        GroupChatService service = new(factory);
        Assert.True(
            service.TryCreateGroupChat(
                "迁移保留群聊",
                members.Select(member => member.Id),
                includesLocalUser: false,
                out GroupChat? groupChat,
                out string errorMessage),
            errorMessage);
        return Assert.IsType<GroupChat>(groupChat);
    }

    private static GroupMessage SaveGroupMessage(
        VocaChatDbContextFactory factory,
        GroupChat groupChat,
        AiAccount sender,
        string content)
    {
        GroupMessage message = new(
            groupChat.Id,
            MessageSenderType.AiAccount,
            sender.Nickname,
            sender.Id,
            content,
            DateTime.Now,
            sequenceNumber: 1);
        using VocaChatDbContext dbContext = factory.CreateDbContext();
        dbContext.GroupMessages.Add(message);
        dbContext.SaveChanges();
        return message;
    }
}

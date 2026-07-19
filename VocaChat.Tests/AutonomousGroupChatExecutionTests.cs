using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证自主好友群聊会复用长期群聊，并让至少三位不同好友留下可持久化消息。
/// </summary>
public sealed class AutonomousGroupChatExecutionTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task ExecuteAsync_WithApprovedGroup_PersistsThreeSpeakersAndReusesGroupChat()
    {
        IReadOnlyList<AiAccount> accounts = CreateStrongGroup();
        RecordingAiMessageGenerator generator = new();
        AutonomousGroupChatExecutionService executionService =
            CreateExecutionService(generator);
        DateTime firstStartedAt = new(2026, 7, 19, 15, 0, 0);

        AutonomousGroupChatExecutionResult firstResult =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                firstStartedAt,
                randomJitter: 10,
                requestedTopic: "周末去哪里");

        Assert.Equal(
            AutonomousGroupChatExecutionStatus.Completed,
            firstResult.Status);
        Assert.True(firstResult.GroupChatCreated);
        Assert.NotNull(firstResult.GroupChat);
        Assert.False(firstResult.GroupChat.IncludesLocalUser);
        Assert.NotNull(firstResult.Session);
        Assert.Equal(
            AutonomousGroupChatSessionStatus.Completed,
            firstResult.Session.Status);
        Assert.Equal(3, firstResult.Session.Participants.Count);
        Assert.Equal(3, generator.Requests.Count);
        Assert.Equal(
            3,
            generator.Requests
                .Select(request => request.Speaker.Id)
                .Distinct()
                .Count());
        Assert.Equal(3, firstResult.Messages.Count);
        Assert.Equal(
            3,
            firstResult.Messages
                .Select(message => message.SenderAiAccountId)
                .Distinct()
                .Count());
        Assert.All(firstResult.Messages, message =>
            Assert.Equal(
                firstResult.Session.Id,
                message.AutonomousGroupChatSessionId));
        Assert.All(generator.Requests, request =>
        {
            Assert.Equal(
                AiMessageGenerationScenario.AutonomousGroupChat,
                request.Scenario);
            Assert.Equal(2, request.OtherParticipants.Count);
        });

        AutonomousGroupChatSession? storedSession =
            new AutonomousGroupChatSessionService(
                _database.CreateDbContextFactory())
                .FindById(firstResult.Session.Id);
        GroupChat? storedGroup = new GroupChatService(
            _database.CreateDbContextFactory())
            .FindById(firstResult.GroupChat.Id);
        IReadOnlyList<GroupMessage> storedMessages = new GroupMessageService(
            _database.CreateDbContextFactory())
            .GetOrderedChatHistory(storedGroup!);
        Assert.NotNull(storedSession);
        Assert.Equal(3, storedSession.Participants.Count);
        Assert.Equal(
            firstResult.Messages.Select(message => message.Id),
            storedMessages.Select(message => message.Id));

        AutonomousGroupChatExecutionResult secondResult =
            await executionService.ExecuteAsync(
                accounts.Select(account => account.Id),
                firstStartedAt.AddHours(1),
                randomJitter: 10,
                requestedTopic: "再聊一次");

        Assert.Equal(
            AutonomousGroupChatExecutionStatus.Completed,
            secondResult.Status);
        Assert.False(secondResult.GroupChatCreated);
        Assert.Equal(firstResult.GroupChat.Id, secondResult.GroupChat!.Id);
        Assert.NotEqual(firstResult.Session.Id, secondResult.Session!.Id);
    }

    private IReadOnlyList<AiAccount> CreateStrongGroup()
    {
        AiAccountService accountService = new(
            _database.CreateDbContextFactory());
        List<AiAccount> accounts = new();
        for (int index = 1; index <= 3; index++)
        {
            Assert.True(accountService.TryCreateAiAccount(
                new AiAccountCreationData
                {
                    Nickname = $"ExecutionFriend{index}",
                    InterestTags = new[] { "摄影" }
                },
                out AiAccount? account,
                out string errorMessage), errorMessage);
            accounts.Add(account!);
        }

        AutonomousInteractionSettingsService settingsService = new(
            _database.CreateDbContextFactory());
        Assert.True(settingsService.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.High,
            allowPrivateChats: true,
            allowGroupChats: true,
            privateChatContinuationRatePercent: 80,
            privateChatMaximumRounds: 6,
            autonomousGroupChatMaximumMembers: 6,
            out _,
            out string settingsError), settingsError);

        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        foreach (AiAccount from in accounts)
        {
            foreach (AiAccount to in accounts.Where(account => account.Id != from.Id))
            {
                Assert.Equal(
                    AiRelationshipOperationStatus.Success,
                    relationshipService.TryUpdateRelationship(
                        from.Id,
                        to.Id,
                        familiarity: 100,
                        affinity: 100,
                        trust: 100,
                        out _));
            }
        }

        return accounts.AsReadOnly();
    }

    private AutonomousGroupChatExecutionService CreateExecutionService(
        IAiMessageGenerator generator)
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        ConversationActionPlanner actionPlanner = new(new Random(7));
        return new AutonomousGroupChatExecutionService(
            new AutonomousGroupChatJudge(factory),
            new AutonomousGroupChatPlanningService(factory),
            new AutonomousGroupChatSpeakerPlanner(factory),
            new AutonomousGroupChatSessionService(factory),
            new AiAccountService(factory),
            new GroupChatService(factory),
            new GroupMessageService(factory),
            new RuleBasedConversationDirector(actionPlanner),
            generator);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

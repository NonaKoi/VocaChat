using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证受控自主私信只在判断通过时保存完整交流并更新双向关系。
/// </summary>
public sealed class AutonomousPrivateChatExecutionServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void RejectedDecision_DoesNotCreateChatOrMessages()
    {
        (AiAccount first, AiAccount second) = CreatePair("Rejected");

        AutonomousPrivateChatExecutionResult result = CreateService().Execute(
            first.Id,
            second.Id,
            new DateTime(2026, 7, 18, 10, 0, 0),
            randomJitter: 10);

        Assert.Equal(
            AutonomousPrivateChatExecutionStatus.DecisionRejected,
            result.Status);
        Assert.Equal(
            AutonomousPrivateChatDecisionStage.GlobalDisabled,
            result.Decision.Stage);
        Assert.Null(result.PrivateChat);
        Assert.Null(new PrivateChatService(_database.CreateDbContextFactory())
            .FindByAiAccountPair(first.Id, second.Id));
    }

    [Fact]
    public void ApprovedDecision_SavesTwoMessagesAndRecordsBothRelationships()
    {
        (AiAccount first, AiAccount second) = CreatePair("Approved");
        EnableAutonomousPrivateChats();
        SetStrongRelationship(first.Id, second.Id);
        DateTime occurredAt = new(2026, 7, 18, 11, 0, 0);

        AutonomousPrivateChatExecutionResult result = CreateService().Execute(
            first.Id,
            second.Id,
            occurredAt,
            randomJitter: 0);

        Assert.Equal(
            AutonomousPrivateChatExecutionStatus.Completed,
            result.Status);
        Assert.True(result.PrivateChatCreated);
        Assert.NotNull(result.PrivateChat);
        Assert.NotNull(result.InitiatorMessage);
        Assert.NotNull(result.RecipientReply);
        Assert.Equal(
            result.Decision.InitiatorAiAccountId,
            result.InitiatorMessage.SenderAiAccountId);
        Assert.Equal(
            result.Decision.RecipientAiAccountId,
            result.RecipientReply.SenderAiAccountId);

        PrivateChatService restartedService = new(
            _database.CreateDbContextFactory());
        IReadOnlyList<PrivateMessage> history =
            restartedService.GetOrderedChatHistory(result.PrivateChat.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal(result.InitiatorMessage.Id, history[0].Id);
        Assert.Equal(result.RecipientReply.Id, history[1].Id);

        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryGetRelationship(
                first.Id,
                second.Id,
                out AiRelationship? firstToSecond));
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryGetRelationship(
                second.Id,
                first.Id,
                out AiRelationship? secondToFirst));
        Assert.Equal(1, firstToSecond!.InteractionCount);
        Assert.Equal(1, secondToFirst!.InteractionCount);
        Assert.Equal(occurredAt, firstToSecond.LastInteractionAt);
        Assert.Equal(occurredAt, secondToFirst.LastInteractionAt);
    }

    [Fact]
    public void ImmediateSecondExecution_IsRejectedByCooldownWithoutExtraMessages()
    {
        (AiAccount first, AiAccount second) = CreatePair("Cooldown");
        EnableAutonomousPrivateChats();
        SetStrongRelationship(first.Id, second.Id);
        DateTime firstRunAt = new(2026, 7, 18, 12, 0, 0);
        AutonomousPrivateChatExecutionService service = CreateService();

        AutonomousPrivateChatExecutionResult firstResult = service.Execute(
            first.Id,
            second.Id,
            firstRunAt,
            randomJitter: 0);
        AutonomousPrivateChatExecutionResult secondResult = service.Execute(
            first.Id,
            second.Id,
            firstRunAt.AddMinutes(1),
            randomJitter: 10);

        Assert.Equal(
            AutonomousPrivateChatExecutionStatus.Completed,
            firstResult.Status);
        Assert.Equal(
            AutonomousPrivateChatExecutionStatus.DecisionRejected,
            secondResult.Status);
        Assert.Equal(
            AutonomousPrivateChatDecisionStage.CooldownActive,
            secondResult.Decision.Stage);
        Assert.Equal(
            2,
            new PrivateChatService(_database.CreateDbContextFactory())
                .GetOrderedChatHistory(firstResult.PrivateChat!.Id)
                .Count);
    }

    private (AiAccount First, AiAccount Second) CreatePair(string prefix)
    {
        return (CreateAccount($"{prefix}A"), CreateAccount($"{prefix}B"));
    }

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private void EnableAutonomousPrivateChats()
    {
        AutonomousInteractionSettingsService service = new(
            _database.CreateDbContextFactory());
        Assert.True(service.TryUpdateSettings(
            isEnabled: true,
            AutonomousInteractionFrequency.Normal,
            allowPrivateChats: true,
            allowGroupChats: false,
            out _,
            out string errorMessage), errorMessage);
    }

    private void SetStrongRelationship(Guid firstId, Guid secondId)
    {
        AiRelationshipService service = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            service.TryUpdateRelationship(
                firstId,
                secondId,
                familiarity: 100,
                affinity: 100,
                trust: 100,
                out _));
    }

    private AutonomousPrivateChatExecutionService CreateService()
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        return new AutonomousPrivateChatExecutionService(
            new AutonomousPrivateChatJudge(factory),
            new AiAccountService(factory),
            new PrivateChatService(factory),
            new FakeAiReplyService(),
            new AiRelationshipService(factory));
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

public sealed class AiInteractionDiagnosticLogServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void RecordedGenerationFailure_CanBeReadByNewServiceInstance()
    {
        Guid accountId = Guid.NewGuid();
        Guid conversationId = Guid.NewGuid();
        AiInteractionDiagnosticLogService first = new(
            _database.CreateDbContextFactory());

        Assert.True(first.TryRecord(
            AiInteractionDiagnosticSeverity.Error,
            AiInteractionDiagnosticCode.MessageGenerationFailed,
            AiMessageGenerationScenario.UserPrivateChat,
            accountId,
            conversationId,
            "回复生成失败。",
            "输出没有满足疑问句策略。"));

        AiInteractionDiagnosticLog log = Assert.Single(
            new AiInteractionDiagnosticLogService(
                _database.CreateDbContextFactory()).GetRecent());
        Assert.Equal(accountId, log.AiAccountId);
        Assert.Equal(conversationId, log.ConversationId);
        Assert.Equal("UserPrivateChat", log.Scenario);
        Assert.Contains("疑问句策略", log.Detail);
    }

    [Fact]
    public void GroupPlanAndRecoveredFailure_AreStoredAsReadableSummaries()
    {
        AiAccount speaker = new(
            "alpha-vc",
            "Alpha",
            string.Empty,
            string.Empty,
            string.Empty);
        GroupChat groupChat = new("诊断群");
        groupChat.AddMember(speaker);
        GroupConversationPlanningRequest request = new()
        {
            GroupChat = groupChat,
            MaximumSpeakerCount = 2,
            MaximumTotalMessageCount = 6
        };
        GroupConversationTurnPlan plan = new()
        {
            TopicFocus = "周末安排",
            TurnGoal = "回应当前话题",
            Speakers = new[]
            {
                new GroupConversationSpeakerPlan
                {
                    SpeakerAiAccountId = speaker.Id,
                    Audience = GroupConversationAudience.LocalUser,
                    Role = GroupConversationRole.DirectAnswer,
                    ResponseGoal = "回答用户",
                    NewContribution = "给出时间建议"
                }
            },
            SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection,
            UsedRuleFallback = true
        };
        GroupConversationDiagnosticService diagnostics = new(
            new AiInteractionDiagnosticLogService(
                _database.CreateDbContextFactory()));

        diagnostics.RecordPlan(request, plan, "模型计划缺少有效发言者");
        diagnostics.RecordFailure(
            AiMessageGenerationScenario.GroupPrimaryReply,
            groupChat.Id,
            speaker.Id,
            "回复生成阶段",
            "模型暂时不可用。",
            wasRecovered: true);

        IReadOnlyList<AiInteractionDiagnosticLog> logs =
            new AiInteractionDiagnosticLogService(
                _database.CreateDbContextFactory()).GetRecent();

        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, log =>
            log.Code == AiInteractionDiagnosticCode.GroupConversationPlanFallback
            && log.Detail.Contains("人数上限：2")
            && log.Detail.Contains("消息上限：6")
            && log.WasRecovered);
        Assert.Contains(logs, log =>
            log.Code == AiInteractionDiagnosticCode.GroupConversationExecutionFailed
            && log.Detail == "模型暂时不可用。"
            && log.WasRecovered);
    }

    public void Dispose() => _database.Dispose();
}

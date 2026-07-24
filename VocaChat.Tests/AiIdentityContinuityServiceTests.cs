using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证个人记忆进入生成请求前的账号隔离、时效筛选和导演建议预验证。
/// </summary>
public sealed class AiIdentityContinuityServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void PrepareGenerationRequest_RecallsOnlyCurrentActiveMemories()
    {
        AiAccount speaker = CreateAccount("ContinuitySpeaker");
        AiAccount other = CreateAccount("ContinuityOther");
        AiSelfMemoryService memoryService = CreateMemoryService();
        DateTime now = new(2026, 7, 20, 12, 0, 0);

        CreateMemory(
            memoryService,
            speaker.Id,
            "最近正在准备秋季插画展",
            salience: 20,
            isUserLocked: true);
        for (int index = 0; index < 7; index++)
        {
            CreateMemory(
                memoryService,
                speaker.Id,
                $"个人偏好记录 {index}",
                salience: 90 - index);
        }
        CreateMemory(
            memoryService,
            speaker.Id,
            "已经过期的短期安排",
            salience: 100,
            validUntil: now.AddDays(-1));
        CreateMemory(
            memoryService,
            other.Id,
            "另一个账号正在准备音乐节",
            salience: 100);

        AiMessageGenerationRequest request = CreateRequest(speaker) with
        {
            FocusContent = "插画展准备得怎么样了"
        };
        AiMessageGenerationRequest prepared = CreateService()
            .PrepareGenerationRequest(request, now);

        Assert.Equal(8, prepared.RelevantSelfMemories.Count);
        Assert.All(prepared.RelevantSelfMemories, memory =>
            Assert.Equal(speaker.Id, memory.AiAccountId));
        Assert.Contains(prepared.RelevantSelfMemories, memory =>
            memory.Summary == "最近正在准备秋季插画展");
        Assert.DoesNotContain(prepared.RelevantSelfMemories, memory =>
            memory.Summary == "已经过期的短期安排");
        Assert.DoesNotContain(prepared.RelevantSelfMemories, memory =>
            memory.Summary == "另一个账号正在准备音乐节");
    }

    [Fact]
    public void PrepareGenerationRequest_KeepsRelevantMemoryBeyondLockedAnchors()
    {
        AiAccount speaker = CreateAccount("ContinuityRelevance");
        AiSelfMemoryService memoryService = CreateMemoryService();
        for (int index = 0; index < 6; index++)
        {
            CreateMemory(
                memoryService,
                speaker.Id,
                $"用户锁定但与当前话题无关的记录 {index}",
                salience: 100 - index,
                isUserLocked: true);
        }
        CreateMemory(
            memoryService,
            speaker.Id,
            "最近正在准备秋季插画展",
            salience: 10);

        AiMessageGenerationRequest prepared = CreateService()
            .PrepareGenerationRequest(
                CreateRequest(speaker) with
                {
                    FocusContent = "秋季插画展准备得怎么样了"
                });

        Assert.Equal(7, prepared.RelevantSelfMemories.Count);
        Assert.Equal(
            7,
            prepared.RelevantSelfMemories.Count(memory =>
                memory.IsProtectedFact));
        Assert.Contains(prepared.RelevantSelfMemories, memory =>
            memory.Summary == "最近正在准备秋季插画展");
    }

    [Fact]
    public void ValidateDirectionPlan_RemovesForeignReferencesAndUnsafeProposals()
    {
        AiAccount speaker = CreateAccount("ContinuityValidation");
        AiSelfMemoryService memoryService = CreateMemoryService();
        AiSelfMemory existing = CreateMemory(
            memoryService,
            speaker.Id,
            "最近正在准备秋季插画展",
            salience: 80);
        AiMessageGenerationRequest prepared = CreateService()
            .PrepareGenerationRequest(CreateRequest(speaker));
        Guid unknownMemoryId = Guid.NewGuid();
        ConversationDirectionPlan rawPlan = new(
            CreateActionPlan(),
            ConversationBeat.Develop,
            "插画展",
            "说明目前的准备进度",
            Guid.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            "补充当前进度",
            Array.Empty<string>(),
            Array.Empty<string>(),
            usedRuleFallback: false,
            selectedMessageCount: 1,
            referencedSelfMemoryIds: new[]
            {
                existing.Id,
                existing.Id,
                unknownMemoryId
            },
            selfMemoryProposals: new[]
            {
                new AiSelfMemoryProposal(
                    AiSelfMemoryProposalOperation.Add,
                    null,
                    speaker.Id,
                    speaker.CharacterWorldId,
                    AiSelfMemoryType.OngoingActivity,
                    "activity.illustration-exhibition",
                    AiSelfMemoryFactNature.Objective,
                    AiSelfMemoryMutability.Mutable,
                    "正在为插画展整理最后一批作品",
                    "本轮准备自然说明当前进度"),
                new AiSelfMemoryProposal(
                    AiSelfMemoryProposalOperation.Add,
                    null,
                    speaker.Id,
                    speaker.CharacterWorldId,
                    AiSelfMemoryType.PersonalFact,
                    "profile.occupation",
                    AiSelfMemoryFactNature.Objective,
                    AiSelfMemoryMutability.Immutable,
                    "突然更换了职业",
                    "稳定身份事实不能由导演自动创建")
            });

        AiIdentityContinuityPlan validated = CreateService()
            .ValidateDirectionPlan(prepared, rawPlan);

        Assert.Equal(
            new[] { existing.Id },
            validated.DirectionPlan.ReferencedSelfMemoryIds);
        AiSelfMemoryProposal accepted = Assert.Single(
            validated.DirectionPlan.SelfMemoryProposals);
        Assert.Equal(
            AiSelfMemoryType.OngoingActivity,
            accepted.Type);
        Assert.Equal(2, validated.Validation.Decisions.Count);
        Assert.Contains(validated.Validation.Decisions, decision =>
            !decision.IsAccepted
            && decision.Proposal.Type == AiSelfMemoryType.PersonalFact);
    }

    private AiIdentityContinuityService CreateService()
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        return new AiIdentityContinuityService(
            new AiSelfMemoryService(factory),
            new AiInteractionDiagnosticLogService(factory));
    }

    private AiSelfMemoryService CreateMemoryService() =>
        new(_database.CreateDbContextFactory());

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            $"{nickname}-{Guid.NewGuid().ToString("N")[..8]}",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private static AiSelfMemory CreateMemory(
        AiSelfMemoryService service,
        Guid aiAccountId,
        string summary,
        int salience,
        bool isUserLocked = false,
        DateTime? validUntil = null)
    {
        AiSelfMemoryOperationStatus status = service.TryCreateUserMemory(
            aiAccountId,
            AiSelfMemoryType.OngoingActivity,
            summary,
            salience,
            isUserLocked,
            occurredAt: null,
            validFrom: null,
            validUntil,
            out AiSelfMemory? memory,
            out string errorMessage);
        Assert.Equal(AiSelfMemoryOperationStatus.Success, status);
        Assert.Equal(string.Empty, errorMessage);
        return Assert.IsType<AiSelfMemory>(memory);
    }

    private static AiMessageGenerationRequest CreateRequest(AiAccount speaker)
    {
        return new AiMessageGenerationRequest
        {
            Scenario = AiMessageGenerationScenario.UserPrivateChat,
            Speaker = speaker,
            FocusContent = "晚上好",
            RecentMessages = Array.Empty<AiDialogueMessage>(),
            ExpectedMessageCount = 1,
            ActionPlan = CreateActionPlan()
        };
    }

    private static ConversationActionPlan CreateActionPlan() => new(
        ConversationAction.Share,
        ConversationMessageLength.Short,
        ConversationDirectness.Direct,
        ConversationQuestionMode.None,
        ConversationEmotionVisibility.Natural,
        ConversationTopicMovement.Stay,
        ConversationPunctuationRhythm.Natural,
        ConversationRelationshipTone.Unknown,
        ConversationRelationshipBalance.Unknown,
        MayOmitObviousContext: true,
        MayLeaveThoughtOpen: true);

    public void Dispose()
    {
        _database.Dispose();
    }
}

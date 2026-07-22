using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证多人群聊上下文始终按照当前发言者和具体回应对象隔离关系与记忆。
/// </summary>
public sealed class GroupConversationContextServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void PrepareGenerationRequest_LoadsOnlyExplicitTargetDirection()
    {
        AiAccount speaker = CreateAccount("Speaker");
        AiAccount target = CreateAccount("Target");
        AiAccount thirdParty = CreateAccount("ThirdParty");
        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryUpdateRelationship(
                speaker.Id,
                target.Id,
                familiarity: 90,
                affinity: 80,
                trust: 70,
                out _));
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryUpdateRelationship(
                target.Id,
                speaker.Id,
                familiarity: 20,
                affinity: -40,
                trust: 20,
                out _));
        CreateDirectionalMemory(
            speaker,
            target,
            "Target 很喜欢周末露营",
            new DateTime(2026, 7, 21, 18, 0, 0));
        CreateDirectionalMemory(
            thirdParty,
            target,
            "Target 曾经和 ThirdParty 一起看展",
            new DateTime(2026, 7, 21, 19, 0, 0));
        CreateSelfMemory(speaker, "正在准备一套露营装备");
        CreateSelfMemory(thirdParty, "正在学习陶艺");
        AiDialogueMessage targetMessage = new(
            target.Nickname,
            "这个周末想去露营",
            MessageSenderType.AiAccount,
            target.Id,
            Guid.NewGuid(),
            new DateTime(2026, 7, 22, 10, 0, 0));
        AiMessageGenerationRequest request = new()
        {
            Scenario = AiMessageGenerationScenario.GroupFollowUpReply,
            Speaker = speaker,
            OtherParticipants = new[] { target, thirdParty },
            Topic = "周末露营",
            FocusContent = targetMessage.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage),
            RecentMessages = new[] { targetMessage }
        };

        AiMessageGenerationRequest prepared = CreateService()
            .PrepareGenerationRequest(request, target);

        Assert.Equal(target.Id, prepared.RelationshipTarget!.Id);
        Assert.Equal(84, prepared.SpeakerToOtherRelationshipScore);
        Assert.Equal(24, prepared.OtherToSpeakerRelationshipScore);
        AiConversationMemory relationshipMemory = Assert.Single(
            prepared.RelevantMemories);
        Assert.Equal(speaker.Id, relationshipMemory.OwnerAiAccountId);
        Assert.Equal(target.Id, relationshipMemory.SubjectAiAccountId);
        Assert.DoesNotContain(
            prepared.RelevantMemories,
            memory => memory.OwnerAiAccountId == thirdParty.Id);
        Assert.All(
            prepared.RelevantSelfMemories,
            memory => Assert.Equal(speaker.Id, memory.AiAccountId));
        Assert.Contains(
            prepared.RelevantSelfMemories,
            memory => memory.Summary.Contains("露营", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildCandidateContexts_KeepsEveryCandidatesFactsSeparate()
    {
        AiAccount alpha = CreateAccount("Alpha");
        AiAccount beta = CreateAccount("Beta");
        AiAccount gamma = CreateAccount("Gamma");
        CreateSelfMemory(alpha, "Alpha 正在整理海边旅行照片");
        CreateSelfMemory(beta, "Beta 最近在练习烘焙");
        CreateDirectionalMemory(
            alpha,
            beta,
            "Alpha 记得 Beta 不喜欢太甜的点心",
            new DateTime(2026, 7, 21, 20, 0, 0));
        GroupChat groupChat = CreateGroupChat(alpha, beta, gamma);
        GroupMessage anchor = new(
            groupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            "@Beta 周末一起准备点心吧",
            new DateTime(2026, 7, 22, 11, 0, 0));
        GroupMessage recentGammaMessage = new(
            groupChat.Id,
            MessageSenderType.AiAccount,
            gamma.Nickname,
            gamma.Id,
            "我周末会去海边",
            new DateTime(2026, 7, 22, 10, 50, 0));
        GroupConversationPlanningRequest request = new()
        {
            GroupChat = groupChat,
            AnchorMessage = anchor,
            RecentMessages = new[] { recentGammaMessage, anchor }
        };
        GroupConversationTurnPlan fallbackPlan = new()
        {
            AnchorMessageId = anchor.Id,
            TopicFocus = anchor.Content,
            TurnGoal = "回应用户",
            Speakers = new[]
            {
                new GroupConversationSpeakerPlan
                {
                    SpeakerAiAccountId = beta.Id,
                    ReplyTargetMessageId = anchor.Id,
                    Audience = GroupConversationAudience.LocalUser,
                    Role = GroupConversationRole.DirectAnswer,
                    ResponseGoal = "回应用户",
                    NewContribution = "说明周末安排"
                }
            },
            SelectionStatus = AiSpeakerSelectionStatus.MentionMatched,
            UsedRuleFallback = true
        };

        IReadOnlyList<GroupConversationCandidateContext> contexts =
            CreateService().BuildCandidateContexts(
                request,
                new[] { alpha, beta, gamma },
                fallbackPlan);

        Assert.Equal(3, contexts.Count);
        Assert.All(contexts, context => Assert.All(
            context.RelevantSelfMemories,
            memory => Assert.Equal(context.AiAccountId, memory.AiAccountId)));
        GroupConversationCandidateContext alphaContext = contexts.Single(
            context => context.AiAccountId == alpha.Id);
        Assert.Contains(
            alphaContext.RelevantSelfMemories,
            memory => memory.Summary.Contains("Alpha", StringComparison.Ordinal));
        Assert.DoesNotContain(
            alphaContext.RelevantSelfMemories,
            memory => memory.Summary.Contains("Beta 最近", StringComparison.Ordinal));
        GroupConversationRelationshipContext alphaToBeta =
            alphaContext.Relationships.Single(relationship =>
                relationship.TargetAiAccountId == beta.Id);
        AiConversationMemory memory = Assert.Single(
            alphaToBeta.RelevantMemories);
        Assert.Equal(alpha.Id, memory.OwnerAiAccountId);
        Assert.Equal(beta.Id, memory.SubjectAiAccountId);
        Assert.All(contexts, context => Assert.True(
            context.Relationships.Count <= 2));
    }

    private GroupConversationContextService CreateService()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiIdentityContinuityService identityContinuityService = new(
            new AiSelfMemoryService(factory),
            new AiInteractionDiagnosticLogService(factory));
        return new GroupConversationContextService(
            factory,
            identityContinuityService);
    }

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            $"{nickname}-{Guid.NewGuid().ToString("N")[..6]}",
            $"{nickname} 的独立身份",
            $"{nickname} 的性格",
            "自然表达",
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private GroupChat CreateGroupChat(params AiAccount[] members)
    {
        GroupChatService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateGroupChat(
            "关系上下文测试群",
            members.Select(member => member.Id),
            out GroupChat? groupChat,
            out string errorMessage), errorMessage);
        return Assert.IsType<GroupChat>(groupChat);
    }

    private void CreateSelfMemory(AiAccount account, string summary)
    {
        AiSelfMemoryService service = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryCreateUserMemory(
                account.Id,
                AiSelfMemoryType.OngoingActivity,
                summary,
                salience: 80,
                isUserLocked: false,
                occurredAt: new DateTime(2026, 7, 21, 12, 0, 0),
                validFrom: null,
                validUntil: null,
                out _,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);
    }

    private void CreateDirectionalMemory(
        AiAccount owner,
        AiAccount subject,
        string summary,
        DateTime endedAt)
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        PrivateChatService privateChatService = new(factory);
        Assert.True(privateChatService.TryGetOrCreateAiPrivateChat(
            owner.Id,
            subject.Id,
            out PrivateChat? privateChat,
            out _,
            out string chatError), chatError);
        AutonomousPrivateChatSessionService sessionService = new(factory);
        Assert.True(sessionService.TryStartSession(
            privateChat!.Id,
            owner.Id,
            subject.Id,
            "关系记忆来源",
            maximumRounds: 2,
            continuationRatePercent: 80,
            endedAt.AddMinutes(-3),
            out AutonomousPrivateChatSession? session,
            out string sessionError), sessionError);
        Assert.True(sessionService.TryStartRound(
            session!.Id,
            isClosing: false,
            occurrenceProbability: 1,
            randomRoll: null,
            AutonomousPrivateChatMessageMode.Single,
            AutonomousPrivateChatMessageMode.None,
            initiatorMessageCount: 1,
            recipientMessageCount: 0,
            endedAt.AddMinutes(-2),
            out AutonomousPrivateChatRound? round,
            out string roundError), roundError);
        Assert.True(sessionService.TryAppendMessage(
            round!.Id,
            owner,
            summary,
            endedAt.AddMinutes(-1),
            out _,
            out _,
            out string messageError), messageError);
        Assert.True(sessionService.TryCompleteRound(
            round.Id,
            endedAt.AddSeconds(-30),
            out _,
            out string roundCompletionError), roundCompletionError);
        Assert.True(sessionService.TryCompleteSession(
            session.Id,
            AutonomousPrivateChatSessionEndReason.NaturalConclusion,
            endedAt,
            out session,
            out sessionError), sessionError);

        AiMemoryService memoryService = new(factory);
        Assert.Equal(
            AiMemoryOperationStatus.Success,
            memoryService.TryCreateMemory(
                owner.Id,
                subject.Id,
                AiMemoryType.SharedExperience,
                summary,
                salience: 85,
                privateChat.Id,
                session!.Id,
                endedAt,
                out _,
                out string memoryError));
        Assert.Equal(string.Empty, memoryError);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

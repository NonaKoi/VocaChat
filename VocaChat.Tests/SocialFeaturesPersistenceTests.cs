using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证好友、私聊和动态使用同一 SQLite 数据源并可跨 Service 实例读取。
/// </summary>
public sealed class SocialFeaturesPersistenceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void CreateAccount_AutomaticallyCreatesPersistentDefaultContact()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory, "小语");

        Contact? contact = new ContactService(factory).FindByAiAccountId(account.Id);

        Assert.NotNull(contact);
        Assert.Equal(ContactGroup.DefaultGroupId, contact.ContactGroupId);
        Assert.Equal(account.Id, contact.AiAccount.Id);
    }

    [Fact]
    public async Task PrivateChat_PersistsUserAndMultipleAiMessagesAcrossServiceInstances()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory, "小语");
        Contact contact = new ContactService(factory).FindByAiAccountId(account.Id)!;
        PrivateChatService firstService = new(factory);
        Assert.True(firstService.TryGetOrCreate(contact.Id, out PrivateChat? chat, out _, out string createError), createError);
        RecordingAiMessageGenerator generator = new();

        PrivateChatInteractionResult result = await new PrivateChatInteractionService(
                firstService,
                generator,
                new RuleBasedConversationDirector(
                    new ConversationActionPlanner()),
                new AiReplyTimingScheduler(
                    factory,
                    (_, _) => Task.CompletedTask),
                new AiReplyMessageCountSettingsResolver(factory),
                new ConversationQuestionPolicyService(factory),
                new AiIdentityContinuityService(
                    new AiSelfMemoryService(factory),
                    new AiInteractionDiagnosticLogService(factory)),
                CreateWorldKnowledgeProcessor(factory),
                CreateWorldConversationContextService(factory))
            .ProcessUserMessageAsync(
                chat!,
                "确实存在平行世界。请分几条具体说说今天怎么一起学习");

        Assert.Equal(PrivateChatInteractionStatus.Succeeded, result.Status);
        IReadOnlyList<PrivateMessage> history = new PrivateChatService(factory).GetOrderedChatHistory(chat!.Id);
        Assert.Equal(MessageSenderType.User, history[0].SenderType);
        Assert.All(
            history.Skip(1),
            message => Assert.Equal(account.Id, message.SenderAiAccountId));
        Assert.InRange(result.AiReplies.Count, 2, 4);
        Assert.Equal(1 + result.AiReplies.Count, history.Count);
        AiMessageGenerationRequest request = Assert.Single(generator.Requests);
        Assert.Equal(1, request.AllowedMessageCountRange!.Minimum);
        Assert.Equal(4, request.AllowedMessageCountRange.Maximum);
        Assert.Equal(result.UserMessage!.Id, request.ReplyTarget!.Message!.MessageId);
        Assert.Equal(ConversationAction.Answer, request.ActionPlan!.Action);
        Assert.NotNull(request.DirectionPlan);
        Assert.True(request.DirectionPlan.UsedRuleFallback);
        Assert.Equal(
            result.AiReplies.Count,
            request.DirectionPlan.SelectedMessageCount);
        Assert.Equal(result.AiReplies.Count, request.ExpectedMessageCount);
        Assert.Equal(
            AiParallelWorldAwarenessState.Informed,
            Assert.IsType<AiWorldConversationContext>(
                request.WorldConversationContext)
                .ParallelWorldAwareness);
        Assert.True(
            request.WorldConversationContext
                .IsNewlyInformedByCurrentMessage);
        using VocaChatDbContext dbContext = factory.CreateDbContext();
        Assert.Equal(
            AiParallelWorldAwarenessState.Informed,
            Assert.Single(
                dbContext.AiParallelWorldAwareness).State);
    }

    [Fact]
    public async Task PrivateChat_AppliesValidatedDirectorMemoryAfterAiMessageIsSaved()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory, "记忆连续性测试账号");
        Contact contact = new ContactService(factory)
            .FindByAiAccountId(account.Id)!;
        PrivateChatService chatService = new(factory);
        Assert.True(chatService.TryGetOrCreate(
            contact.Id,
            out PrivateChat? chat,
            out _,
            out string createError), createError);
        AiSelfMemoryProposal proposal = new(
            AiSelfMemoryProposalOperation.Add,
            null,
            account.Id,
            account.CharacterWorldId,
            AiSelfMemoryType.OngoingActivity,
            "activity.autumn-illustration-exhibition",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Mutable,
            "最近正在准备秋季插画展",
            "本轮回复将明确表达持续中的准备事项");
        PrivateChatInteractionService interactionService = new(
            chatService,
            new StaticMessageGenerator("我最近正在准备秋季插画展"),
            new StaticDirector(proposal),
            new AiReplyTimingScheduler(factory, (_, _) => Task.CompletedTask),
            new AiReplyMessageCountSettingsResolver(factory),
            new ConversationQuestionPolicyService(factory),
            new AiIdentityContinuityService(
                new AiSelfMemoryService(factory),
                new StaticAiSelfMemorySemanticJudge(),
                new AiInteractionDiagnosticLogService(factory)));

        PrivateChatInteractionResult result = await interactionService
            .ProcessUserMessageAsync(chat!, "最近在忙什么？");

        Assert.Equal(PrivateChatInteractionStatus.Succeeded, result.Status);
        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            new AiSelfMemoryService(factory).TryGetActiveContextMemories(
                account.Id,
                10,
                out IReadOnlyList<AiSelfMemory> memories,
                out string memoryError));
        Assert.Equal(string.Empty, memoryError);
        AiSelfMemory memory = Assert.Single(memories);
        PrivateMessage reply = Assert.Single(result.AiReplies);
        Assert.Equal(AiSelfMemorySource.Director, memory.Source);
        Assert.Equal(chat!.Id, memory.SourceConversationId);
        Assert.Equal(reply.Id, memory.SourceMessageId);
    }

    [Fact]
    public async Task PrivateChat_ExtractsMemoryCandidateWhenDirectorReturnsNone()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory, "保存后候选提取测试账号");
        Contact contact = new ContactService(factory)
            .FindByAiAccountId(account.Id)!;
        PrivateChatService chatService = new(factory);
        Assert.True(chatService.TryGetOrCreate(
            contact.Id,
            out PrivateChat? chat,
            out _,
            out string createError), createError);
        StaticAiSelfMemorySemanticJudge semanticJudge = new();
        PrivateChatInteractionService interactionService = new(
            chatService,
            new StaticMessageGenerator("我准备下个月办一场小型插画展。"),
            new StaticDirector(),
            new AiReplyTimingScheduler(factory, (_, _) => Task.CompletedTask),
            new AiReplyMessageCountSettingsResolver(factory),
            new ConversationQuestionPolicyService(factory),
            new AiIdentityContinuityService(
                new AiSelfMemoryService(factory),
                semanticJudge,
                new AiInteractionDiagnosticLogService(factory)));

        PrivateChatInteractionResult result = await interactionService
            .ProcessUserMessageAsync(chat!, "下个月有什么打算？");

        Assert.Equal(PrivateChatInteractionStatus.Succeeded, result.Status);
        Assert.Single(semanticJudge.Requests);
        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            new AiSelfMemoryService(factory).TryGetActiveContextMemories(
                account.Id,
                10,
                out IReadOnlyList<AiSelfMemory> memories,
                out string memoryError));
        Assert.Equal(string.Empty, memoryError);
        AiSelfMemory memory = Assert.Single(memories);
        Assert.Equal(AiSelfMemoryType.Plan, memory.Type);
        Assert.Equal(
            "我准备下个月办一场小型插画展。",
            memory.Summary);
        Assert.Equal(
            Assert.Single(result.AiReplies).Id,
            memory.SourceMessageId);
    }

    [Fact]
    public async Task PrivateChat_RejectedMemoryProposal_DoesNotDiscardSavedMessages()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory, "记忆拒绝测试账号");
        Contact contact = new ContactService(factory)
            .FindByAiAccountId(account.Id)!;
        PrivateChatService chatService = new(factory);
        Assert.True(chatService.TryGetOrCreate(
            contact.Id,
            out PrivateChat? chat,
            out _,
            out string createError), createError);
        AiSelfMemoryProposal invalidProposal = new(
            AiSelfMemoryProposalOperation.Add,
            null,
            account.Id,
            account.CharacterWorldId,
            AiSelfMemoryType.PersonalFact,
            "profile.birthplace",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Immutable,
            "出生于一座未经用户确认的城市",
            "导演试图新增稳定身份事实");
        PrivateChatInteractionService interactionService = new(
            chatService,
            new StaticMessageGenerator("这个名字听起来有点熟。"),
            new StaticDirector(invalidProposal),
            new AiReplyTimingScheduler(factory, (_, _) => Task.CompletedTask),
            new AiReplyMessageCountSettingsResolver(factory),
            new ConversationQuestionPolicyService(factory),
            new AiIdentityContinuityService(
                new AiSelfMemoryService(factory),
                new StaticAiSelfMemorySemanticJudge(),
                new AiInteractionDiagnosticLogService(factory)));

        PrivateChatInteractionResult result = await interactionService
            .ProcessUserMessageAsync(chat!, "你听说过镜海港吗？");

        Assert.Equal(PrivateChatInteractionStatus.Succeeded, result.Status);
        IReadOnlyList<PrivateMessage> history =
            new PrivateChatService(factory).GetOrderedChatHistory(chat!.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal("你听说过镜海港吗？", history[0].Content);
        Assert.Equal("这个名字听起来有点熟。", history[1].Content);
        new AiSelfMemoryService(factory).TryGetActiveContextMemories(
            account.Id,
            10,
            out IReadOnlyList<AiSelfMemory> memories,
            out _);
        Assert.Empty(memories);
        AiInteractionDiagnosticLog log = Assert.Single(
            new AiInteractionDiagnosticLogService(factory).GetRecent());
        Assert.Equal(AiInteractionDiagnosticCode.SelfMemoryDecision, log.Code);
        Assert.Contains("拒绝 1 项", log.Detail);
    }

    [Fact]
    public void Post_LikeAndCommentRemainVisibleAfterServiceRecreation()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory, "小语");
        PostService service = new(factory);
        Assert.True(service.TryCreatePost(account.Id, "记录今天的第一条动态。", out Post? post, out string createError), createError);
        Assert.True(service.TryAddLocalUserLike(post!.Id, out string likeError), likeError);
        Assert.True(service.TryAddLocalUserComment(post.Id, "欢迎来到动态！", out _, out string commentError), commentError);

        Post storedPost = new PostService(factory).FindById(post.Id)!;

        Assert.Single(storedPost.Likes);
        Assert.Single(storedPost.Comments);
        Assert.Equal("欢迎来到动态！", storedPost.Comments[0].Content);
    }

    private static AiAccount CreateAccount(VocaChatDbContextFactory factory, string nickname)
    {
        AiAccountService service = new(factory);
        Assert.True(service.TryCreateAiAccount(nickname, string.Empty, string.Empty, string.Empty, out AiAccount? account, out string error), error);
        return account!;
    }

    private static AiWorldKnowledgeMessageProcessor
        CreateWorldKnowledgeProcessor(VocaChatDbContextFactory factory)
    {
        return new AiWorldKnowledgeMessageProcessor(
            factory,
            new AiWorldKnowledgeCandidateExtractor(),
            new AiWorldKnowledgeService(factory),
            new AiWorldAwarenessService(factory));
    }

    private static AiWorldConversationContextService
        CreateWorldConversationContextService(
            VocaChatDbContextFactory factory)
    {
        return new AiWorldConversationContextService(
            factory,
            new AiWorldAwarenessService(factory),
            new AiWorldKnowledgeService(factory),
            new AiWorldKnowledgeCandidateExtractor());
    }

    private sealed class StaticMessageGenerator : IAiMessageGenerator
    {
        private readonly string _content;

        public StaticMessageGenerator(string content)
        {
            _content = content;
        }

        public Task<IReadOnlyList<string>> GenerateMessagesAsync(
            AiMessageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<string>>(new[] { _content });
        }
    }

    private sealed class StaticDirector : IConversationDirector
    {
        private readonly IReadOnlyList<AiSelfMemoryProposal> _proposals;

        public StaticDirector(params AiSelfMemoryProposal[] proposals)
        {
            _proposals = proposals;
        }

        public Task<ConversationDirectionPlan> CreatePlanAsync(
            AiMessageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConversationActionPlan actionPlan = new(
                ConversationAction.Answer,
                ConversationMessageLength.Short,
                ConversationDirectness.Direct,
                ConversationQuestionMode.None,
                ConversationEmotionVisibility.Natural,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Unknown,
                ConversationRelationshipBalance.Unknown,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: false);
            return Task.FromResult(new ConversationDirectionPlan(
                actionPlan,
                ConversationBeat.Develop,
                "近期安排",
                "说明当前正在做的事情",
                request.ReplyTarget?.Message?.MessageId ?? Guid.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                "补充当前持续事项",
                Array.Empty<string>(),
                Array.Empty<string>(),
                usedRuleFallback: false,
                selectedMessageCount: 1,
                referencedSelfMemoryIds: Array.Empty<Guid>(),
                selfMemoryProposals: _proposals));
        }
    }

    public void Dispose() => _database.Dispose();
}

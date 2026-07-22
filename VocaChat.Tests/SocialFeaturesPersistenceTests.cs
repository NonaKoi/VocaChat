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
                    new AiInteractionDiagnosticLogService(factory)))
            .ProcessUserMessageAsync(chat!, "请分几条具体说说今天怎么一起学习");

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
            AiSelfMemoryType.OngoingActivity,
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
        private readonly AiSelfMemoryProposal _proposal;

        public StaticDirector(AiSelfMemoryProposal proposal)
        {
            _proposal = proposal;
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
                selfMemoryProposals: new[] { _proposal }));
        }
    }

    public void Dispose() => _database.Dispose();
}

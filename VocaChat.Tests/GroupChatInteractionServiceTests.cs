using System;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证用户消息、AI 选择、假回复和消息保存组合后的完整交互流程。
/// </summary>
public sealed class GroupChatInteractionServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task ProcessUserMessage_WithValidContent_SavesUserAndAiMessages()
    {
        TestContext context = CreateContext("Alpha");

        GroupChatInteractionResult result = await context.InteractionService
            .ProcessUserMessageAsync(context.GroupChat, "大家好");
        IReadOnlyList<GroupMessage> history = context.MessageService
            .GetOrderedChatHistory(context.GroupChat);

        Assert.Equal(GroupChatInteractionStatus.Succeeded, result.Status);
        Assert.Equal(
            AiSpeakerSelectionStatus.DefaultSelection,
            result.SpeakerSelectionStatus);
        Assert.NotNull(result.UserMessage);
        GroupMessage aiReply = Assert.Single(result.AiReplies);
        Assert.Equal(2, history.Count);
        Assert.Equal(MessageSenderType.User, history[0].SenderType);
        Assert.Equal(MessageSenderType.AiAccount, history[1].SenderType);
        Assert.NotNull(result.UserMessage.InteractionBatchId);
        Assert.Equal(
            result.UserMessage.InteractionBatchId,
            aiReply.InteractionBatchId);
        Assert.Null(result.UserMessage.ReplyToMessageId);
        Assert.Equal(result.UserMessage.Id, aiReply.ReplyToMessageId);
        Assert.Contains(
            context.GroupChat.Members,
            member => member.Id == aiReply.SenderAiAccountId);
    }

    [Fact]
    public async Task ProcessUserMessage_WithMemberMention_SelectsMentionedMember()
    {
        TestContext context = CreateContext("Alpha", "Beta");
        AiAccount mentionedMember = context.GroupChat.Members.Single(
            member => member.Nickname == "Beta");

        GroupChatInteractionResult result = await context.InteractionService
            .ProcessUserMessageAsync(context.GroupChat, "请让 @Beta 回复");

        Assert.Equal(GroupChatInteractionStatus.Succeeded, result.Status);
        Assert.Equal(
            AiSpeakerSelectionStatus.MentionMatched,
            result.SpeakerSelectionStatus);
        Assert.Equal(
            mentionedMember.Id,
            Assert.Single(result.AiReplies).SenderAiAccountId);
    }

    [Fact]
    public async Task ProcessUserMessage_WithUnmatchedMention_UsesDefaultSelection()
    {
        TestContext context = CreateContext("Alpha");

        GroupChatInteractionResult result = await context.InteractionService
            .ProcessUserMessageAsync(context.GroupChat, "@Gamma 你好");

        Assert.Equal(GroupChatInteractionStatus.Succeeded, result.Status);
        Assert.Equal(
            AiSpeakerSelectionStatus.MentionNotMatched,
            result.SpeakerSelectionStatus);
        GroupMessage aiReply = Assert.Single(result.AiReplies);
        Assert.Contains(
            context.GroupChat.Members,
            member => member.Id == aiReply.SenderAiAccountId);
    }

    [Fact]
    public async Task ProcessUserMessage_WithTwoSpeakers_UsesExplicitSequentialTargets()
    {
        TestContext context = CreateContext("Alpha", "Beta", "Gamma");
        RecordingAiMessageGenerator generator = new();
        GroupChatReplyPlanner replyPlanner = new(
            _database.CreateDbContextFactory());
        SequentialGroupConversationDirector groupDirector = new();
        AiAccount firstMember = context.GroupChat.Members[0];
        AiAccount secondMember = context.GroupChat.Members[1];
        AiRelationshipService relationshipService = new(
            _database.CreateDbContextFactory());
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryUpdateRelationship(
                secondMember.Id,
                firstMember.Id,
                familiarity: 90,
                affinity: 80,
                trust: 70,
                out _));
        Assert.Equal(
            AiRelationshipOperationStatus.Success,
            relationshipService.TryUpdateRelationship(
                firstMember.Id,
                secondMember.Id,
                familiarity: 20,
                affinity: -40,
                trust: 20,
                out _));
        GroupChatInteractionService interactionService = new(
            context.MessageService,
            generator,
            replyPlanner,
            groupDirector,
            new GroupConversationPlanValidator(),
            new RuleBasedConversationDirector(
                new ConversationActionPlanner(new ConstantRandom(0.5))),
            CreateNoDelayScheduler(),
            new AiReplyMessageCountSettingsResolver(
                _database.CreateDbContextFactory()),
            new ConversationQuestionPolicyService(
                _database.CreateDbContextFactory()),
            CreateIdentityContinuityService(),
            CreateConversationContextService(),
            new GroupConversationDensitySettingsResolver(
                _database.CreateDbContextFactory()),
            new GroupConversationDiagnosticService(
                new AiInteractionDiagnosticLogService(
                    _database.CreateDbContextFactory())));

        GroupChatInteractionResult result = await interactionService
            .ProcessUserMessageAsync(
                context.GroupChat,
                "@Alpha @Beta 你们觉得今天去哪？");

        Assert.Equal(GroupChatInteractionStatus.Succeeded, result.Status);
        Assert.Equal(2, generator.Requests.Count);
        AiMessageGenerationRequest primaryRequest = generator.Requests[0];
        AiMessageGenerationRequest followUpRequest = generator.Requests[1];
        Assert.Equal(
            GroupConversationRole.DirectAnswer,
            primaryRequest.GroupConversationPlan!.Role);
        Assert.Equal(
            GroupConversationRole.Complement,
            followUpRequest.GroupConversationPlan!.Role);
        Assert.Equal(
            ConversationAction.Answer,
            primaryRequest.ActionPlan!.Action);
        Assert.Equal(
            ConversationAction.Share,
            followUpRequest.ActionPlan!.Action);
        GroupConversationPlanningRequest planningRequest = Assert.Single(
            groupDirector.Requests);
        Assert.Equal(3, planningRequest.MaximumSpeakerCount);
        Assert.Equal(6, planningRequest.MaximumTotalMessageCount);
        Assert.Equal(1, primaryRequest.AllowedMessageCountRange!.Minimum);
        Assert.Equal(4, primaryRequest.AllowedMessageCountRange.Maximum);
        Assert.Equal(1, followUpRequest.AllowedMessageCountRange!.Minimum);
        Assert.Equal(
            Math.Min(4, 6 - primaryRequest.ExpectedMessageCount),
            followUpRequest.AllowedMessageCountRange.Maximum);
        Assert.True(generator.Requests.Sum(request => request.ExpectedMessageCount) <= 6);
        Assert.Equal(
            generator.Requests.Sum(request => request.ExpectedMessageCount),
            result.AiReplies.Count);
        GroupMessage finalPrimaryReply = result.AiReplies[
            primaryRequest.ExpectedMessageCount - 1];
        Assert.Equal(
            result.UserMessage!.Id,
            primaryRequest.ReplyTarget!.Message!.MessageId);
        Assert.Equal(
            finalPrimaryReply.Id,
            followUpRequest.ReplyTarget!.Message!.MessageId);
        Assert.Equal(
            finalPrimaryReply.SenderAiAccountId,
            followUpRequest.ReplyTarget.Message.SenderAiAccountId);
        Assert.Null(primaryRequest.RelationshipTarget);
        Assert.Null(primaryRequest.SpeakerToOtherRelationshipScore);
        Assert.Equal(firstMember.Id, followUpRequest.RelationshipTarget!.Id);
        Assert.Equal(84, followUpRequest.SpeakerToOtherRelationshipScore);
        Assert.Equal(24, followUpRequest.OtherToSpeakerRelationshipScore);
        Assert.Equal(
            result.UserMessage.Id,
            followUpRequest.ConversationAnchor!.MessageId);
        Assert.Equal(
            result.UserMessage.Content,
            followUpRequest.ConversationAnchor.Content);
        Assert.NotEqual(
            result.UserMessage.Id,
            followUpRequest.ReplyTarget.Message.MessageId);
        Assert.All(result.AiReplies, reply => Assert.Equal(
            result.UserMessage.InteractionBatchId,
            reply.InteractionBatchId));
        Assert.All(
            result.AiReplies.Take(primaryRequest.ExpectedMessageCount),
            reply => Assert.Equal(
                result.UserMessage.Id,
                reply.ReplyToMessageId));
        Assert.All(
            result.AiReplies.Skip(primaryRequest.ExpectedMessageCount),
            reply => Assert.Equal(
                finalPrimaryReply.Id,
                reply.ReplyToMessageId));
    }

    [Fact]
    public async Task ProcessUserMessage_WithBlankContent_RejectsBeforeAiReply()
    {
        TestContext context = CreateContext("Alpha");

        GroupChatInteractionResult result = await context.InteractionService
            .ProcessUserMessageAsync(context.GroupChat, "   ");

        Assert.Equal(
            GroupChatInteractionStatus.UserMessageRejected,
            result.Status);
        Assert.Equal(
            AiSpeakerSelectionStatus.NotAttempted,
            result.SpeakerSelectionStatus);
        Assert.Null(result.UserMessage);
        Assert.Empty(result.AiReplies);
        Assert.Empty(context.MessageService.GetOrderedChatHistory(context.GroupChat));
    }

    [Fact]
    public async Task ProcessUserMessage_WhenAiReplyCannotBeSaved_KeepsUserMessage()
    {
        TestContext context = CreateContext("Alpha");
        string maximumLengthUserMessage = new(
            'a',
            GroupMessage.ContentMaxLength);

        GroupChatInteractionResult result = await context.InteractionService
            .ProcessUserMessageAsync(context.GroupChat, maximumLengthUserMessage);
        GroupMessage storedMessage = Assert.Single(
            context.MessageService.GetOrderedChatHistory(context.GroupChat));

        Assert.Equal(GroupChatInteractionStatus.AiReplyFailed, result.Status);
        Assert.NotNull(result.UserMessage);
        Assert.Empty(result.AiReplies);
        Assert.Equal(result.UserMessage.Id, storedMessage.Id);
        Assert.Equal(MessageSenderType.User, storedMessage.SenderType);
    }

    [Fact]
    public async Task ProcessUserMessage_WhenSecondReplyCannotBeSaved_ReturnsSavedFirstReply()
    {
        VocaChat.Data.VocaChatDbContextFactory dbContextFactory =
            _database.CreateDbContextFactory();
        AiAccountService accountService = new(dbContextFactory);
        Assert.True(accountService.TryCreateAiAccount(
            "Alpha",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? firstAccount,
            out string firstAccountError), firstAccountError);
        Assert.True(accountService.TryCreateAiAccount(
            "Beta",
            string.Empty,
            string.Empty,
            new string('s', 200),
            out AiAccount? secondAccount,
            out string secondAccountError), secondAccountError);
        AiAccount primarySpeaker = Assert.IsType<AiAccount>(firstAccount);
        AiAccount followUpSpeaker = Assert.IsType<AiAccount>(secondAccount);
        GroupChatService groupChatService = new(dbContextFactory);
        Assert.True(groupChatService.TryCreateGroupChat(
            "部分回复测试群",
            new[] { primarySpeaker.Id, followUpSpeaker.Id },
            out GroupChat? groupChat,
            out string groupError), groupError);
        GroupChat storedGroupChat = Assert.IsType<GroupChat>(groupChat);
        FakeAiReplyService fakeAiReplyService = new();
        int primaryOverhead = fakeAiReplyService.GenerateReply(
            primarySpeaker,
            string.Empty).Length;
        int followUpOverhead = fakeAiReplyService.GenerateFollowUpReply(
            followUpSpeaker,
            primarySpeaker,
            string.Empty).Length;
        Assert.True(followUpOverhead > primaryOverhead);
        string mentions = "@Alpha @Beta ";
        string content = mentions + new string(
            'a',
            GroupMessage.ContentMaxLength - primaryOverhead - mentions.Length);
        GroupMessageService messageService = new(dbContextFactory);
        GroupChatReplyPlanner replyPlanner = new(dbContextFactory);
        GroupChatInteractionService interactionService = new(
            messageService,
            fakeAiReplyService,
            replyPlanner,
            new RuleBasedGroupConversationDirector(replyPlanner),
            new GroupConversationPlanValidator(),
            new RuleBasedConversationDirector(
                new ConversationActionPlanner()),
            CreateNoDelayScheduler(),
            new AiReplyMessageCountSettingsResolver(dbContextFactory),
            new ConversationQuestionPolicyService(dbContextFactory),
            CreateIdentityContinuityService(),
            CreateConversationContextService(),
            new GroupConversationDensitySettingsResolver(dbContextFactory),
            new GroupConversationDiagnosticService(
                new AiInteractionDiagnosticLogService(dbContextFactory)));

        GroupChatInteractionResult result = await interactionService
            .ProcessUserMessageAsync(storedGroupChat, content);
        IReadOnlyList<GroupMessage> history = messageService
            .GetOrderedChatHistory(storedGroupChat);

        Assert.Equal(
            GroupChatInteractionStatus.PartiallySucceeded,
            result.Status);
        Assert.NotNull(result.UserMessage);
        GroupMessage savedReply = Assert.Single(result.AiReplies);
        Assert.Equal(primarySpeaker.Id, savedReply.SenderAiAccountId);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, message => message.Id == savedReply.Id);
    }

    /// <summary>
    /// 使用独立 SQLite 数据库创建具有指定群成员的交互测试上下文。
    /// </summary>
    private TestContext CreateContext(params string[] memberNicknames)
    {
        VocaChat.Data.VocaChatDbContextFactory dbContextFactory =
            _database.CreateDbContextFactory();
        AiAccountService accountService = new(dbContextFactory);
        List<Guid> memberIds = new();

        foreach (string nickname in memberNicknames)
        {
            bool accountCreated = accountService.TryCreateAiAccount(
                nickname,
                string.Empty,
                string.Empty,
                string.Empty,
                out AiAccount? account,
                out string accountError);

            Assert.True(accountCreated, accountError);
            memberIds.Add(Assert.IsType<AiAccount>(account).Id);
        }

        GroupChatService groupChatService = new(dbContextFactory);
        bool groupCreated = groupChatService.TryCreateGroupChat(
            "交互测试群",
            memberIds,
            out GroupChat? groupChat,
            out string groupError);
        Assert.True(groupCreated, groupError);

        GroupMessageService messageService = new(dbContextFactory);
        GroupChatReplyPlanner replyPlanner = new(dbContextFactory);
        GroupChatInteractionService interactionService = new(
            messageService,
            new FakeAiReplyService(),
            replyPlanner,
            new RuleBasedGroupConversationDirector(replyPlanner),
            new GroupConversationPlanValidator(),
            new RuleBasedConversationDirector(
                new ConversationActionPlanner()),
            CreateNoDelayScheduler(),
            new AiReplyMessageCountSettingsResolver(dbContextFactory),
            new ConversationQuestionPolicyService(dbContextFactory),
            CreateIdentityContinuityService(),
            CreateConversationContextService(),
            new GroupConversationDensitySettingsResolver(dbContextFactory),
            new GroupConversationDiagnosticService(
                new AiInteractionDiagnosticLogService(dbContextFactory)));

        return new TestContext(
            Assert.IsType<GroupChat>(groupChat),
            messageService,
            interactionService);
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private AiReplyTimingScheduler CreateNoDelayScheduler()
    {
        return new AiReplyTimingScheduler(
            _database.CreateDbContextFactory(),
            (_, _) => Task.CompletedTask);
    }

    private AiIdentityContinuityService CreateIdentityContinuityService()
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        return new AiIdentityContinuityService(
            new AiSelfMemoryService(factory),
            new AiInteractionDiagnosticLogService(factory));
    }

    private GroupConversationContextService CreateConversationContextService()
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        return new GroupConversationContextService(
            factory,
            new AiIdentityContinuityService(
                new AiSelfMemoryService(factory),
                new AiInteractionDiagnosticLogService(factory)));
    }

    private sealed class TestContext
    {
        public GroupChat GroupChat { get; }
        public GroupMessageService MessageService { get; }
        public GroupChatInteractionService InteractionService { get; }

        public TestContext(
            GroupChat groupChat,
            GroupMessageService messageService,
            GroupChatInteractionService interactionService)
        {
            GroupChat = groupChat;
            MessageService = messageService;
            InteractionService = interactionService;
        }
    }

    private sealed class ConstantRandom : Random
    {
        private readonly double _value;

        public ConstantRandom(double value)
        {
            _value = value;
        }

        protected override double Sample() => _value;
    }

    private sealed class SequentialGroupConversationDirector
        : IGroupConversationDirector
    {
        public List<GroupConversationPlanningRequest> Requests { get; } = new();

        public Task<GroupConversationTurnPlan> CreatePlanAsync(
            GroupConversationPlanningRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            AiAccount first = request.GroupChat.Members[0];
            AiAccount second = request.GroupChat.Members[1];
            GroupMessage anchorMessage = request.AnchorMessage!;
            GroupConversationTurnPlan plan = new()
            {
                AnchorMessageId = anchorMessage.Id,
                TopicFocus = anchorMessage.Content,
                TurnGoal = "先回应用户，再让另一位成员补充",
                UnresolvedGoals = new[] { "回应当前用户消息" },
                Speakers = new GroupConversationSpeakerPlan[]
                {
                    new()
                    {
                        SpeakerAiAccountId = first.Id,
                        ReplyTargetMessageId = anchorMessage.Id,
                        Audience = GroupConversationAudience.LocalUser,
                        Role = GroupConversationRole.DirectAnswer,
                        ResponseGoal = "直接回答用户",
                        NewContribution = "给出第一项判断"
                    },
                    new()
                    {
                        SpeakerAiAccountId = second.Id,
                        ReplyTargetMessageId = anchorMessage.Id,
                        TargetAiAccountId = first.Id,
                        Audience = GroupConversationAudience.SpecificAiAccount,
                        Role = GroupConversationRole.Complement,
                        ResponseGoal = "回应第一位成员并补充",
                        NewContribution = "给出不同的第二项判断"
                    }
                },
                SelectionStatus = AiSpeakerSelectionStatus.MentionMatched
            };
            return Task.FromResult(plan);
        }
    }
}

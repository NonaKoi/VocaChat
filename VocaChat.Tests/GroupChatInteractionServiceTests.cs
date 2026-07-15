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
    public void ProcessUserMessage_WithValidContent_SavesUserAndAiMessages()
    {
        TestContext context = CreateContext("Alpha");

        GroupChatInteractionResult result = context.InteractionService
            .ProcessUserMessage(context.GroupChat, "大家好");
        IReadOnlyList<GroupMessage> history = context.MessageService
            .GetOrderedChatHistory(context.GroupChat);

        Assert.Equal(GroupChatInteractionStatus.Succeeded, result.Status);
        Assert.Equal(
            AiSpeakerSelectionStatus.DefaultSelection,
            result.SpeakerSelectionStatus);
        Assert.NotNull(result.UserMessage);
        Assert.NotNull(result.AiReply);
        Assert.Equal(2, history.Count);
        Assert.Equal(MessageSenderType.User, history[0].SenderType);
        Assert.Equal(MessageSenderType.AiAccount, history[1].SenderType);
        Assert.Contains(
            context.GroupChat.Members,
            member => member.Id == result.AiReply.SenderAiAccountId);
    }

    [Fact]
    public void ProcessUserMessage_WithMemberMention_SelectsMentionedMember()
    {
        TestContext context = CreateContext("Alpha", "Beta");
        AiAccount mentionedMember = context.GroupChat.Members.Single(
            member => member.Nickname == "Beta");

        GroupChatInteractionResult result = context.InteractionService
            .ProcessUserMessage(context.GroupChat, "请让 @Beta 回复");

        Assert.Equal(GroupChatInteractionStatus.Succeeded, result.Status);
        Assert.Equal(
            AiSpeakerSelectionStatus.MentionMatched,
            result.SpeakerSelectionStatus);
        Assert.Equal(mentionedMember.Id, result.AiReply?.SenderAiAccountId);
    }

    [Fact]
    public void ProcessUserMessage_WithUnmatchedMention_UsesDefaultSelection()
    {
        TestContext context = CreateContext("Alpha");

        GroupChatInteractionResult result = context.InteractionService
            .ProcessUserMessage(context.GroupChat, "@Gamma 你好");

        Assert.Equal(GroupChatInteractionStatus.Succeeded, result.Status);
        Assert.Equal(
            AiSpeakerSelectionStatus.MentionNotMatched,
            result.SpeakerSelectionStatus);
        Assert.Equal(
            context.GroupChat.Members[0].Id,
            result.AiReply?.SenderAiAccountId);
    }

    [Fact]
    public void ProcessUserMessage_WithBlankContent_RejectsBeforeAiReply()
    {
        TestContext context = CreateContext("Alpha");

        GroupChatInteractionResult result = context.InteractionService
            .ProcessUserMessage(context.GroupChat, "   ");

        Assert.Equal(
            GroupChatInteractionStatus.UserMessageRejected,
            result.Status);
        Assert.Equal(
            AiSpeakerSelectionStatus.NotAttempted,
            result.SpeakerSelectionStatus);
        Assert.Null(result.UserMessage);
        Assert.Null(result.AiReply);
        Assert.Empty(context.MessageService.GetOrderedChatHistory(context.GroupChat));
    }

    [Fact]
    public void ProcessUserMessage_WhenAiReplyCannotBeSaved_KeepsUserMessage()
    {
        TestContext context = CreateContext("Alpha");
        string maximumLengthUserMessage = new(
            'a',
            GroupMessage.ContentMaxLength);

        GroupChatInteractionResult result = context.InteractionService
            .ProcessUserMessage(context.GroupChat, maximumLengthUserMessage);
        GroupMessage storedMessage = Assert.Single(
            context.MessageService.GetOrderedChatHistory(context.GroupChat));

        Assert.Equal(GroupChatInteractionStatus.AiReplyFailed, result.Status);
        Assert.NotNull(result.UserMessage);
        Assert.Null(result.AiReply);
        Assert.Equal(result.UserMessage.Id, storedMessage.Id);
        Assert.Equal(MessageSenderType.User, storedMessage.SenderType);
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
        GroupChatInteractionService interactionService = new(
            messageService,
            new FakeAiReplyService());

        return new TestContext(
            Assert.IsType<GroupChat>(groupChat),
            messageService,
            interactionService);
    }

    public void Dispose()
    {
        _database.Dispose();
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
}

using System;
using System.Collections.Generic;
using System.Linq;
using VocaChat.ConsoleApp.Data;
using VocaChat.ConsoleApp.Models;
using VocaChat.ConsoleApp.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证用户和 AI 群消息的数据库保存、成员限制、隔离和稳定排序。
/// </summary>
public class GroupMessageServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void TrySaveUserMessage_WithValidContent_PersistsCorrectUserMessage()
    {
        TestContext context = CreateContext();

        bool succeeded = context.MessageService.TrySaveUserMessage(
            context.GroupChat,
            "  hello  ",
            out GroupMessage? message,
            out string errorMessage);

        GroupMessage storedMessage = Assert.Single(
            CreateMessageService().GetOrderedChatHistory(context.GroupChat));

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(message);
        Assert.Equal(message.Id, storedMessage.Id);
        Assert.Equal(context.GroupChat.Id, storedMessage.GroupChatId);
        Assert.Equal(MessageSenderType.User, storedMessage.SenderType);
        Assert.Equal("我", storedMessage.SenderDisplayName);
        Assert.Null(storedMessage.SenderAiAccountId);
        Assert.Equal("hello", storedMessage.Content);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TrySaveUserMessage_WithBlankContent_Fails(string content)
    {
        TestContext context = CreateContext();

        bool succeeded = context.MessageService.TrySaveUserMessage(
            context.GroupChat,
            content,
            out GroupMessage? message,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(message);
        Assert.Equal("消息内容不能为空。", errorMessage);
        Assert.Empty(context.MessageService.GetOrderedChatHistory(context.GroupChat));
    }

    [Fact]
    public void TrySaveUserMessage_WithMissingGroupChat_Fails()
    {
        TestContext context = CreateContext();
        GroupChat missingGroupChat = new("Missing");

        bool succeeded = context.MessageService.TrySaveUserMessage(
            missingGroupChat,
            "hello",
            out GroupMessage? message,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(message);
        Assert.Equal("群聊不存在，不能保存消息。", errorMessage);
    }

    [Fact]
    public void TrySaveAiReply_WithGroupMember_PersistsCorrectAiMessage()
    {
        TestContext context = CreateContext();

        bool succeeded = context.MessageService.TrySaveAiReply(
            context.GroupChat,
            context.JoinedAccount,
            "  reply  ",
            out GroupMessage? message,
            out string errorMessage);

        GroupMessage storedMessage = Assert.Single(
            CreateMessageService().GetOrderedChatHistory(context.GroupChat));

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(message);
        Assert.Equal(message.Id, storedMessage.Id);
        Assert.Equal(MessageSenderType.AiAccount, storedMessage.SenderType);
        Assert.Equal(context.JoinedAccount.Nickname, storedMessage.SenderDisplayName);
        Assert.Equal(context.JoinedAccount.Id, storedMessage.SenderAiAccountId);
        Assert.Equal("reply", storedMessage.Content);
    }

    [Fact]
    public void TrySaveAiReply_WithUnjoinedAccount_FailsWithoutSavingMessage()
    {
        TestContext context = CreateContext();

        bool succeeded = context.MessageService.TrySaveAiReply(
            context.GroupChat,
            context.UnjoinedAccount,
            "reply",
            out GroupMessage? message,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(message);
        Assert.Equal("未加入当前群聊的 AI 账号不能发送群消息。", errorMessage);
        Assert.Empty(context.MessageService.GetOrderedChatHistory(context.GroupChat));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TrySaveAiReply_WithBlankContent_Fails(string content)
    {
        TestContext context = CreateContext();

        bool succeeded = context.MessageService.TrySaveAiReply(
            context.GroupChat,
            context.JoinedAccount,
            content,
            out GroupMessage? message,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(message);
        Assert.Equal("AI 回复内容不能为空。", errorMessage);
        Assert.Empty(context.MessageService.GetOrderedChatHistory(context.GroupChat));
    }

    [Fact]
    public void GetOrderedChatHistory_AfterCreatingNewService_ReturnsUserAndAiMessages()
    {
        TestContext context = CreateContext();
        context.MessageService.TrySaveUserMessage(
            context.GroupChat,
            "hello",
            out GroupMessage? userMessage,
            out _);
        context.MessageService.TrySaveAiReply(
            context.GroupChat,
            context.JoinedAccount,
            "reply",
            out GroupMessage? aiMessage,
            out _);

        GroupMessageService reloadedMessageService = CreateMessageService();
        IReadOnlyList<GroupMessage> history =
            reloadedMessageService.GetOrderedChatHistory(context.GroupChat);

        Assert.Equal(2, history.Count);
        Assert.Contains(history, message => message.Id == userMessage?.Id);
        Assert.Contains(history, message => message.Id == aiMessage?.Id);
    }

    [Fact]
    public void ExistingGroupChat_AfterServicesRestart_CanContinueWithoutDuplicatingData()
    {
        VocaChatDbContextFactory firstFactory =
            _database.CreateDbContextFactory();
        AiAccountService firstAccountService = new(firstFactory);
        AiAccount account = CreateAccount(firstAccountService, "Alpha");
        GroupChatService firstGroupChatService = new(firstFactory);
        GroupChat createdGroupChat = CreateGroupChat(
            firstGroupChatService,
            "Persistent Team",
            account.Id);
        GroupMessageService firstMessageService = new(firstFactory);
        bool oldMessageSaved = firstMessageService.TrySaveUserMessage(
            createdGroupChat,
            "old message",
            out GroupMessage? oldMessage,
            out string oldMessageError);
        Assert.True(oldMessageSaved, oldMessageError);

        GroupChatService restartedGroupChatService = new(
            _database.CreateDbContextFactory());
        GroupChat existingGroupChat = Assert.Single(
            restartedGroupChatService.GetAllGroupChats());
        GroupMessageService restartedMessageService = new(
            _database.CreateDbContextFactory());
        IReadOnlyList<GroupMessage> historyBeforeContinuing =
            restartedMessageService.GetOrderedChatHistory(existingGroupChat);
        FakeAiReplyService fakeAiReplyService = new();
        AiAccount? aiSpeaker = fakeAiReplyService.SelectAiSpeaker(
            existingGroupChat,
            "new message",
            out _);
        AiAccount selectedAiSpeaker = Assert.IsType<AiAccount>(aiSpeaker);

        Assert.Equal(createdGroupChat.Id, existingGroupChat.Id);
        Assert.Single(existingGroupChat.Members);
        Assert.Equal(oldMessage?.Id, Assert.Single(historyBeforeContinuing).Id);

        bool newMessageSaved = restartedMessageService.TrySaveUserMessage(
            existingGroupChat,
            "new message",
            out GroupMessage? newMessage,
            out string newMessageError);
        Assert.True(newMessageSaved, newMessageError);
        string fakeReply = fakeAiReplyService.GenerateReply(
            selectedAiSpeaker,
            "new message");
        bool aiReplySaved = restartedMessageService.TrySaveAiReply(
            existingGroupChat,
            selectedAiSpeaker,
            fakeReply,
            out GroupMessage? aiMessage,
            out string aiMessageError);
        Assert.True(aiReplySaved, aiMessageError);

        AiAccountService finalAccountService = new(
            _database.CreateDbContextFactory());
        GroupChatService finalGroupChatService = new(
            _database.CreateDbContextFactory());
        GroupMessageService finalMessageService = new(
            _database.CreateDbContextFactory());
        IReadOnlyList<GroupMessage> completeHistory =
            finalMessageService.GetOrderedChatHistory(existingGroupChat);

        Assert.Equal(3, completeHistory.Count);
        Assert.Contains(completeHistory, message => message.Id == oldMessage?.Id);
        Assert.Contains(completeHistory, message => message.Id == newMessage?.Id);
        Assert.Contains(completeHistory, message => message.Id == aiMessage?.Id);
        Assert.Single(finalAccountService.GetAllAccounts());
        Assert.Single(finalGroupChatService.GetAllGroupChats());
        Assert.Single(finalGroupChatService.GetMembers(existingGroupChat));
    }

    [Fact]
    public void GetOrderedChatHistory_DoesNotMixMessagesFromDifferentGroupChats()
    {
        TestContext context = CreateContext();
        GroupChat secondGroupChat = CreateGroupChat(
            context.GroupChatService,
            "Second Team",
            context.JoinedAccount.Id);
        context.MessageService.TrySaveUserMessage(
            context.GroupChat,
            "first group",
            out _,
            out _);
        context.MessageService.TrySaveUserMessage(
            secondGroupChat,
            "second group",
            out _,
            out _);

        IReadOnlyList<GroupMessage> firstHistory =
            context.MessageService.GetOrderedChatHistory(context.GroupChat);
        IReadOnlyList<GroupMessage> secondHistory =
            context.MessageService.GetOrderedChatHistory(secondGroupChat);

        Assert.Equal("first group", Assert.Single(firstHistory).Content);
        Assert.Equal("second group", Assert.Single(secondHistory).Content);
    }

    [Fact]
    public void GetOrderedChatHistory_OrdersBySentAtAndUsesIdForStableTies()
    {
        TestContext context = CreateContext();
        DateTime sameTime = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        GroupMessage firstSameTimeMessage = new(
            context.GroupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            "same time one",
            sameTime);
        GroupMessage secondSameTimeMessage = new(
            context.GroupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            "same time two",
            sameTime);
        GroupMessage laterMessage = new(
            context.GroupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            "later",
            sameTime.AddMinutes(1));

        using (VocaChatDbContext dbContext =
               _database.CreateDbContextFactory().CreateDbContext())
        {
            dbContext.GroupMessages.AddRange(
                laterMessage,
                secondSameTimeMessage,
                firstSameTimeMessage);
            dbContext.SaveChanges();
        }

        IReadOnlyList<GroupMessage> history =
            context.MessageService.GetOrderedChatHistory(context.GroupChat);
        IReadOnlyList<GroupMessage> reloadedHistory =
            CreateMessageService().GetOrderedChatHistory(context.GroupChat);
        Guid[] tiedMessageIds = history
            .Take(2)
            .Select(message => message.Id)
            .ToArray();

        Assert.Equal(laterMessage.Id, history[2].Id);
        Assert.Contains(firstSameTimeMessage.Id, tiedMessageIds);
        Assert.Contains(secondSameTimeMessage.Id, tiedMessageIds);
        Assert.Equal(
            history.Select(message => message.Id),
            reloadedHistory.Select(message => message.Id));
    }

    /// <summary>
    /// 创建包含一个群内账号和一个未入群账号的独立数据库测试上下文。
    /// </summary>
    private TestContext CreateContext()
    {
        VocaChatDbContextFactory dbContextFactory =
            _database.CreateDbContextFactory();
        AiAccountService accountService = new(dbContextFactory);
        AiAccount joinedAccount = CreateAccount(accountService, "Alpha");
        AiAccount unjoinedAccount = CreateAccount(accountService, "Beta");
        GroupChatService groupChatService = new(dbContextFactory);
        GroupChat groupChat = CreateGroupChat(
            groupChatService,
            "Team",
            joinedAccount.Id);

        return new TestContext(
            groupChat,
            joinedAccount,
            unjoinedAccount,
            groupChatService,
            new GroupMessageService(dbContextFactory));
    }

    /// <summary>
    /// 使用同一个临时数据库创建新的消息 Service，模拟重新启动后的查询。
    /// </summary>
    private GroupMessageService CreateMessageService()
    {
        return new GroupMessageService(_database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private static AiAccount CreateAccount(AiAccountService service, string nickname)
    {
        bool succeeded = service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private static GroupChat CreateGroupChat(
        GroupChatService service,
        string name,
        Guid memberId)
    {
        bool succeeded = service.TryCreateGroupChat(
            name,
            new[] { memberId },
            out GroupChat? groupChat,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        return Assert.IsType<GroupChat>(groupChat);
    }

    private sealed class TestContext
    {
        public GroupChat GroupChat { get; }
        public AiAccount JoinedAccount { get; }
        public AiAccount UnjoinedAccount { get; }
        public GroupChatService GroupChatService { get; }
        public GroupMessageService MessageService { get; }

        public TestContext(
            GroupChat groupChat,
            AiAccount joinedAccount,
            AiAccount unjoinedAccount,
            GroupChatService groupChatService,
            GroupMessageService messageService)
        {
            GroupChat = groupChat;
            JoinedAccount = joinedAccount;
            UnjoinedAccount = unjoinedAccount;
            GroupChatService = groupChatService;
            MessageService = messageService;
        }
    }
}

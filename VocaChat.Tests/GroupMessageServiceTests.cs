using System;
using System.Collections.Generic;
using VocaChat.ConsoleApp.Models;
using VocaChat.ConsoleApp.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证用户和 AI 群消息的保存规则、成员限制和时间排序。
/// </summary>
public class GroupMessageServiceTests
{
    [Fact]
    public void TrySaveUserMessage_WithValidContent_SavesCorrectUserMessage()
    {
        TestContext context = CreateContext();

        bool succeeded = context.MessageService.TrySaveUserMessage(
            context.GroupChat,
            "  hello  ",
            out GroupMessage? message,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(message);
        Assert.Equal(context.GroupChat.Id, message.GroupChatId);
        Assert.Equal(MessageSenderType.User, message.SenderType);
        Assert.Equal("我", message.SenderDisplayName);
        Assert.Null(message.SenderAiAccountId);
        Assert.Equal("hello", message.Content);
        Assert.Same(message, Assert.Single(context.GroupChat.Messages));
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
        Assert.Empty(context.GroupChat.Messages);
    }

    [Fact]
    public void TrySaveAiReply_WithGroupMember_SavesCorrectAiMessage()
    {
        TestContext context = CreateContext();
        context.MessageService.TrySaveUserMessage(
            context.GroupChat,
            "hello",
            out GroupMessage? userMessage,
            out _);

        bool succeeded = context.MessageService.TrySaveAiReply(
            context.GroupChat,
            context.JoinedAccount,
            "  reply  ",
            out GroupMessage? aiMessage,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(aiMessage);
        Assert.Equal(MessageSenderType.AiAccount, aiMessage.SenderType);
        Assert.Equal(context.JoinedAccount.Nickname, aiMessage.SenderDisplayName);
        Assert.Equal(context.JoinedAccount.Id, aiMessage.SenderAiAccountId);
        Assert.Equal("reply", aiMessage.Content);
        Assert.Equal(2, context.GroupChat.Messages.Count);
        Assert.Same(userMessage, context.GroupChat.Messages[0]);
        Assert.Same(aiMessage, context.GroupChat.Messages[1]);
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
        Assert.Empty(context.GroupChat.Messages);
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
        Assert.Empty(context.GroupChat.Messages);
    }

    [Fact]
    public void GetOrderedChatHistory_OrdersBySentAtInsteadOfInsertionOrder()
    {
        TestContext context = CreateContext();
        DateTime earlierTime = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime laterTime = earlierTime.AddMinutes(1);
        GroupMessage laterMessage = new(
            context.GroupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            "later",
            laterTime);
        GroupMessage earlierMessage = new(
            context.GroupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            "earlier",
            earlierTime);
        context.GroupChat.AddMessage(laterMessage);
        context.GroupChat.AddMessage(earlierMessage);

        IReadOnlyList<GroupMessage> history =
            context.MessageService.GetOrderedChatHistory(context.GroupChat);

        Assert.Collection(
            history,
            first => Assert.Same(earlierMessage, first),
            second => Assert.Same(laterMessage, second));
    }

    [Fact]
    public void Messages_CannotBeModifiedByExternalCode()
    {
        TestContext context = CreateContext();
        IList<GroupMessage> mutableView =
            Assert.IsAssignableFrom<IList<GroupMessage>>(context.GroupChat.Messages);
        GroupMessage externalMessage = new(
            context.GroupChat.Id,
            MessageSenderType.User,
            "我",
            null,
            "external");

        Assert.Throws<NotSupportedException>(() => mutableView.Add(externalMessage));
        Assert.Empty(context.GroupChat.Messages);
    }

    /// <summary>
    /// 创建包含一个群内账号和一个未入群账号的独立测试上下文。
    /// </summary>
    private static TestContext CreateContext()
    {
        AiAccountService accountService = new();
        AiAccount joinedAccount = CreateAccount(accountService, "Alpha");
        AiAccount unjoinedAccount = CreateAccount(accountService, "Beta");
        GroupChatService groupChatService = new(accountService);
        bool groupCreated = groupChatService.TryCreateGroupChat(
            "Team",
            new[] { joinedAccount.Id },
            out GroupChat? groupChat,
            out string groupError);
        Assert.True(groupCreated, groupError);

        return new TestContext(
            Assert.IsType<GroupChat>(groupChat),
            joinedAccount,
            unjoinedAccount,
            new GroupMessageService(groupChatService));
    }

    /// <summary>
    /// 创建测试所需账号，并确保测试准备本身成功。
    /// </summary>
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

    private sealed class TestContext
    {
        public GroupChat GroupChat { get; }
        public AiAccount JoinedAccount { get; }
        public AiAccount UnjoinedAccount { get; }
        public GroupMessageService MessageService { get; }

        public TestContext(
            GroupChat groupChat,
            AiAccount joinedAccount,
            AiAccount unjoinedAccount,
            GroupMessageService messageService)
        {
            GroupChat = groupChat;
            JoinedAccount = joinedAccount;
            UnjoinedAccount = unjoinedAccount;
            MessageService = messageService;
        }
    }
}

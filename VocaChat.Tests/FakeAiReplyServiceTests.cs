using System;
using VocaChat.ConsoleApp.Models;
using VocaChat.ConsoleApp.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证假 AI 发言者选择范围、点名优先级和本地回复生成。
/// </summary>
public class FakeAiReplyServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void SelectAiSpeaker_WithoutMention_ReturnsOneCurrentMember()
    {
        GroupContext context = CreateGroupContext("Alpha", "Beta");
        FakeAiReplyService service = new();

        AiAccount? speaker = service.SelectAiSpeaker(
            context.GroupChat,
            "hello",
            out bool selectedByMention);

        Assert.NotNull(speaker);
        Assert.False(selectedByMention);
        Assert.Contains(speaker, context.GroupChat.Members);
    }

    [Fact]
    public void SelectAiSpeaker_WithOnlyOneMember_ReturnsThatMember()
    {
        GroupContext context = CreateGroupContext("Alpha");
        FakeAiReplyService service = new();

        AiAccount? speaker = service.SelectAiSpeaker(
            context.GroupChat,
            "hello",
            out bool selectedByMention);

        Assert.Equal(context.Accounts[0].Id, speaker?.Id);
        Assert.False(selectedByMention);
    }

    [Fact]
    public void SelectAiSpeaker_WithMemberMention_PrefersMentionedMemberIgnoringCase()
    {
        GroupContext context = CreateGroupContext("Alpha", "Beta");
        FakeAiReplyService service = new();

        AiAccount? speaker = service.SelectAiSpeaker(
            context.GroupChat,
            "请让 @bEtA 回复",
            out bool selectedByMention);

        Assert.Equal(context.Accounts[1].Id, speaker?.Id);
        Assert.True(selectedByMention);
    }

    [Fact]
    public void SelectAiSpeaker_WithUnjoinedMention_DoesNotSelectUnjoinedAccount()
    {
        AiAccountService accountService = CreateAccountService();
        AiAccount joinedAccount = CreateAccount(accountService, "Alpha");
        AiAccount unjoinedAccount = CreateAccount(accountService, "Gamma");
        GroupChatService groupChatService = CreateGroupChatService();
        GroupChat groupChat = CreateGroupChat(groupChatService, joinedAccount.Id);
        FakeAiReplyService service = new();

        AiAccount? speaker = service.SelectAiSpeaker(
            groupChat,
            "@Gamma hello",
            out bool selectedByMention);

        Assert.NotNull(speaker);
        Assert.NotEqual(unjoinedAccount.Id, speaker.Id);
        Assert.Contains(speaker, groupChat.Members);
        Assert.False(selectedByMention);
    }

    [Fact]
    public void SelectAiSpeaker_WithoutMembers_ReturnsNull()
    {
        GroupChat emptyGroupChat = new("Empty");
        FakeAiReplyService service = new();

        AiAccount? speaker = service.SelectAiSpeaker(
            emptyGroupChat,
            "hello",
            out bool selectedByMention);

        Assert.Null(speaker);
        Assert.False(selectedByMention);
    }

    [Fact]
    public void GenerateReply_ReturnsNonBlankLocalReplyWithSpeakerAndUserContent()
    {
        AiAccount aiSpeaker = new("Alpha", "助手", "冷静", "简洁");
        FakeAiReplyService service = new();

        string reply = service.GenerateReply(aiSpeaker, "hello");

        Assert.False(string.IsNullOrWhiteSpace(reply));
        Assert.Contains("Alpha", reply);
        Assert.Contains("hello", reply);
        Assert.Contains("模拟回复", reply);
    }

    /// <summary>
    /// 创建成员全部来自账号 Service 的测试群聊。
    /// </summary>
    private GroupContext CreateGroupContext(params string[] nicknames)
    {
        AiAccountService accountService = CreateAccountService();
        List<AiAccount> accounts = new();

        foreach (string nickname in nicknames)
        {
            accounts.Add(CreateAccount(accountService, nickname));
        }

        GroupChatService groupChatService = CreateGroupChatService();
        GroupChat groupChat = CreateGroupChat(
            groupChatService,
            accounts.Select(account => account.Id).ToArray());

        return new GroupContext(groupChat, accounts);
    }

    private AiAccountService CreateAccountService()
    {
        return new AiAccountService(_database.CreateDbContextFactory());
    }

    private GroupChatService CreateGroupChatService()
    {
        return new GroupChatService(_database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
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

    /// <summary>
    /// 使用已有账号创建测试群聊，并确保测试准备本身成功。
    /// </summary>
    private static GroupChat CreateGroupChat(
        GroupChatService service,
        params Guid[] memberIds)
    {
        bool succeeded = service.TryCreateGroupChat(
            "Team",
            memberIds,
            out GroupChat? groupChat,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        return Assert.IsType<GroupChat>(groupChat);
    }

    private sealed class GroupContext
    {
        public GroupChat GroupChat { get; }
        public IReadOnlyList<AiAccount> Accounts { get; }

        public GroupContext(
            GroupChat groupChat,
            IReadOnlyList<AiAccount> accounts)
        {
            GroupChat = groupChat;
            Accounts = accounts;
        }
    }
}

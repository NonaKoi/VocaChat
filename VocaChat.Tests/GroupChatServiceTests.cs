using System;
using System.Collections.Generic;
using VocaChat.ConsoleApp.Models;
using VocaChat.ConsoleApp.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证群聊创建、已有账号成员关系和集合保护。
/// </summary>
public class GroupChatServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void TryCreateGroupChat_WithExistingAccounts_UsesTheirIdsAndStoresChat()
    {
        AiAccountService accountService = CreateAccountService();
        AiAccount firstAccount = CreateAccount(accountService, "Alpha");
        AiAccount secondAccount = CreateAccount(accountService, "Beta");
        GroupChatService groupChatService = new(accountService);
        int accountCountBeforeCreation = accountService.GetAllAccounts().Count;

        bool succeeded = groupChatService.TryCreateGroupChat(
            "  Team  ",
            new[] { firstAccount.Id, secondAccount.Id },
            out GroupChat? groupChat,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(groupChat);
        Assert.Equal("Team", groupChat.Name);
        Assert.Equal(firstAccount.Id, groupChat.Members[0].Id);
        Assert.Equal(secondAccount.Id, groupChat.Members[1].Id);
        Assert.Equal(accountCountBeforeCreation, accountService.GetAllAccounts().Count);
        Assert.Same(groupChat, groupChatService.FindById(groupChat.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreateGroupChat_WithBlankName_Fails(string groupName)
    {
        AiAccountService accountService = CreateAccountService();
        AiAccount account = CreateAccount(accountService, "Alpha");
        GroupChatService groupChatService = new(accountService);

        bool succeeded = groupChatService.TryCreateGroupChat(
            groupName,
            new[] { account.Id },
            out GroupChat? groupChat,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Null(groupChat);
        Assert.Equal("群聊名称不能为空。", errorMessage);
        Assert.Empty(groupChatService.GetAllGroupChats());
    }

    [Fact]
    public void TryAddMember_WithExistingAccount_AddsMember()
    {
        AiAccountService accountService = CreateAccountService();
        AiAccount firstAccount = CreateAccount(accountService, "Alpha");
        AiAccount secondAccount = CreateAccount(accountService, "Beta");
        GroupChatService groupChatService = new(accountService);
        GroupChat groupChat = CreateGroupChat(groupChatService, firstAccount.Id);

        bool succeeded = groupChatService.TryAddMember(
            groupChat,
            secondAccount.Id,
            out string errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.Equal(2, groupChat.Members.Count);
        Assert.Equal(secondAccount.Id, groupChat.Members[1].Id);
    }

    [Fact]
    public void TryAddMember_WithDuplicateAccount_Fails()
    {
        AiAccountService accountService = CreateAccountService();
        AiAccount account = CreateAccount(accountService, "Alpha");
        GroupChatService groupChatService = new(accountService);
        GroupChat groupChat = CreateGroupChat(groupChatService, account.Id);

        bool succeeded = groupChatService.TryAddMember(
            groupChat,
            account.Id,
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Equal("该 AI 账号已经是群成员。", errorMessage);
        Assert.Single(groupChat.Members);
    }

    [Fact]
    public void IsMember_DistinguishesJoinedAndUnjoinedAccounts()
    {
        AiAccountService accountService = CreateAccountService();
        AiAccount joinedAccount = CreateAccount(accountService, "Alpha");
        AiAccount unjoinedAccount = CreateAccount(accountService, "Beta");
        GroupChatService groupChatService = new(accountService);
        GroupChat groupChat = CreateGroupChat(groupChatService, joinedAccount.Id);

        Assert.True(groupChatService.IsMember(groupChat, joinedAccount.Id));
        Assert.False(groupChatService.IsMember(groupChat, unjoinedAccount.Id));
    }

    [Fact]
    public void TryAddMember_WithMissingAccount_Fails()
    {
        AiAccountService accountService = CreateAccountService();
        AiAccount account = CreateAccount(accountService, "Alpha");
        GroupChatService groupChatService = new(accountService);
        GroupChat groupChat = CreateGroupChat(groupChatService, account.Id);

        bool succeeded = groupChatService.TryAddMember(
            groupChat,
            Guid.NewGuid(),
            out string errorMessage);

        Assert.False(succeeded);
        Assert.Equal("AI 账号不存在，不能加入群聊。", errorMessage);
        Assert.Single(groupChat.Members);
    }

    [Fact]
    public void Members_CannotBeModifiedByExternalCode()
    {
        AiAccountService accountService = CreateAccountService();
        AiAccount account = CreateAccount(accountService, "Alpha");
        GroupChatService groupChatService = new(accountService);
        GroupChat groupChat = CreateGroupChat(groupChatService, account.Id);
        IList<AiAccount> mutableView =
            Assert.IsAssignableFrom<IList<AiAccount>>(groupChat.Members);
        AiAccount externalAccount = new("External", string.Empty, string.Empty, string.Empty);

        Assert.Throws<NotSupportedException>(() => mutableView.Add(externalAccount));
        Assert.Single(groupChatService.GetMembers(groupChat));
    }

    private AiAccountService CreateAccountService()
    {
        return new AiAccountService(_database.CreateDbContextFactory());
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
}

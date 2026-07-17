using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证四类会话的参与关系、唯一性和本地用户发言边界。
/// </summary>
public sealed class ConversationParticipationTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void AiPrivateChat_NormalizesPairAndPersistsWithoutReverseDuplicate()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount firstAccount = CreateAccount(factory, "Alpha");
        AiAccount secondAccount = CreateAccount(factory, "Beta");
        PrivateChatService firstService = new(factory);

        Assert.True(firstService.TryGetOrCreateAiPrivateChat(
            secondAccount.Id,
            firstAccount.Id,
            out PrivateChat? createdChat,
            out bool created,
            out string createError), createError);
        Assert.True(created);

        PrivateChatService restartedService = new(factory);
        Assert.True(restartedService.TryGetOrCreateAiPrivateChat(
            firstAccount.Id,
            secondAccount.Id,
            out PrivateChat? existingChat,
            out bool createdAgain,
            out string findError), findError);

        Assert.False(createdAgain);
        Assert.Equal(createdChat!.Id, existingChat!.Id);
        Assert.Equal(PrivateChatKind.AiAccounts, existingChat.Kind);
        Assert.Null(existingChat.ContactId);
        Assert.Equal(2, restartedService.GetAiParticipants(existingChat).Count);
    }

    [Fact]
    public void AiPrivateChat_RejectsSameOrMissingParticipant()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory, "Alpha");
        PrivateChatService service = new(factory);

        Assert.False(service.TryGetOrCreateAiPrivateChat(
            account.Id,
            account.Id,
            out _,
            out _,
            out _));
        Assert.False(service.TryGetOrCreateAiPrivateChat(
            account.Id,
            Guid.NewGuid(),
            out _,
            out _,
            out _));
    }

    [Fact]
    public void AiOnlyConversations_RejectLocalUserMessagesButAllowAiMembers()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount firstAccount = CreateAccount(factory, "Alpha");
        AiAccount secondAccount = CreateAccount(factory, "Beta");
        PrivateChatService privateChatService = new(factory);

        Assert.True(privateChatService.TryGetOrCreateAiPrivateChat(
            firstAccount.Id,
            secondAccount.Id,
            out PrivateChat? privateChat,
            out _,
            out string privateChatError), privateChatError);
        Assert.False(privateChatService.TrySaveUserMessage(
            privateChat!,
            "这条消息不应保存",
            out _,
            out _));
        Assert.True(privateChatService.TrySaveAiReply(
            privateChat!,
            firstAccount,
            "来自好友的消息",
            out _,
            out string aiMessageError), aiMessageError);

        GroupChatService groupChatService = new(factory);
        Assert.True(groupChatService.TryCreateGroupChat(
            "好友群聊",
            new[] { firstAccount.Id, secondAccount.Id },
            includesLocalUser: false,
            out GroupChat? groupChat,
            out string groupError), groupError);
        GroupMessageService groupMessageService = new(factory);

        Assert.False(groupMessageService.TrySaveUserMessage(
            groupChat!,
            "这条群消息不应保存",
            out _,
            out _));
        Assert.True(groupMessageService.TrySaveAiReply(
            groupChat!,
            secondAccount,
            "好友群聊中的消息",
            out _,
            out string groupAiError), groupAiError);
    }

    [Fact]
    public void ConversationSummary_ReturnsAllFourStableCategories()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount firstAccount = CreateAccount(factory, "Alpha");
        AiAccount secondAccount = CreateAccount(factory, "Beta");
        Contact firstContact = new ContactService(factory)
            .FindByAiAccountId(firstAccount.Id)!;
        PrivateChatService privateChatService = new(factory);

        Assert.True(privateChatService.TryGetOrCreate(
            firstContact.Id,
            out _,
            out _,
            out string myPrivateError), myPrivateError);
        Assert.True(privateChatService.TryGetOrCreateAiPrivateChat(
            firstAccount.Id,
            secondAccount.Id,
            out _,
            out _,
            out string friendPrivateError), friendPrivateError);

        GroupChatService groupChatService = new(factory);
        Assert.True(groupChatService.TryCreateGroupChat(
            "我的群聊",
            new[] { firstAccount.Id },
            includesLocalUser: true,
            out _,
            out string myGroupError), myGroupError);
        Assert.True(groupChatService.TryCreateGroupChat(
            "好友群聊",
            new[] { firstAccount.Id, secondAccount.Id },
            includesLocalUser: false,
            out _,
            out string friendGroupError), friendGroupError);

        IReadOnlyList<ConversationSummary> summaries =
            new ConversationService(factory).GetRecentConversations();

        Assert.Contains(summaries,
            summary => summary.Category == ConversationCategory.MyPrivateChat);
        Assert.Contains(summaries,
            summary => summary.Category == ConversationCategory.FriendPrivateChat);
        Assert.Contains(summaries,
            summary => summary.Category == ConversationCategory.MyGroupChat);
        Assert.Contains(summaries,
            summary => summary.Category == ConversationCategory.FriendGroupChat);
    }

    private static AiAccount CreateAccount(
        VocaChatDbContextFactory factory,
        string nickname)
    {
        AiAccountService service = new(factory);
        Assert.True(service.TryCreateAiAccount(
            nickname,
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return account!;
    }

    public void Dispose() => _database.Dispose();
}

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
    public void PrivateChat_PersistsUserAndAiMessagesAcrossServiceInstances()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccount account = CreateAccount(factory, "小语");
        Contact contact = new ContactService(factory).FindByAiAccountId(account.Id)!;
        PrivateChatService firstService = new(factory);
        Assert.True(firstService.TryGetOrCreate(contact.Id, out PrivateChat? chat, out _, out string createError), createError);

        PrivateChatInteractionResult result = new PrivateChatInteractionService(firstService, new FakeAiReplyService())
            .ProcessUserMessage(chat!, "今天一起学习吗？");

        Assert.Equal(PrivateChatInteractionStatus.Succeeded, result.Status);
        IReadOnlyList<PrivateMessage> history = new PrivateChatService(factory).GetOrderedChatHistory(chat!.Id);
        Assert.Collection(history,
            message => Assert.Equal(MessageSenderType.User, message.SenderType),
            message => Assert.Equal(account.Id, message.SenderAiAccountId));
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

    public void Dispose() => _database.Dispose();
}

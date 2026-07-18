using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证群聊发言规划只选择当前成员，并正确处理点名、轮换和关系跟进。
/// </summary>
public sealed class GroupChatReplyPlannerTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void CreatePlan_WithoutMention_SelectsOneCurrentMemberWhenFollowUpIsSkipped()
    {
        GroupContext context = CreateGroupContext("Alpha", "Beta");

        GroupChatReplyPlan plan = context.Planner.CreatePlan(
            context.GroupChat,
            "hello",
            1);

        GroupChatReplyCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal(GroupChatReplyRole.Primary, candidate.Role);
        Assert.Contains(
            context.GroupChat.Members,
            member => member.Id == candidate.Speaker.Id);
        Assert.Equal(AiSpeakerSelectionStatus.DefaultSelection, plan.SelectionStatus);
    }

    [Fact]
    public void CreatePlan_WithTwoMemberMentions_SelectsBothInMentionOrder()
    {
        GroupContext context = CreateGroupContext("Alpha", "Beta", "Gamma");

        GroupChatReplyPlan plan = context.Planner.CreatePlan(
            context.GroupChat,
            "请 @Beta 先说，@Alpha 再补充",
            1);

        Assert.Equal(2, plan.Candidates.Count);
        Assert.Equal("Beta", plan.Candidates[0].Speaker.Nickname);
        Assert.Equal(GroupChatReplyRole.Primary, plan.Candidates[0].Role);
        Assert.Equal("Alpha", plan.Candidates[1].Speaker.Nickname);
        Assert.Equal(GroupChatReplyRole.FollowUp, plan.Candidates[1].Role);
        Assert.Equal(AiSpeakerSelectionStatus.MentionMatched, plan.SelectionStatus);
    }

    [Fact]
    public void CreatePlan_WithUnjoinedMention_NeverSelectsUnjoinedAccount()
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccountService accountService = new(factory);
        AiAccount joinedAccount = CreateAccount(accountService, "Alpha");
        AiAccount unjoinedAccount = CreateAccount(accountService, "Gamma");
        GroupChat groupChat = CreateGroupChat(
            new GroupChatService(factory),
            joinedAccount.Id);

        GroupChatReplyPlan plan = new GroupChatReplyPlanner(factory).CreatePlan(
            groupChat,
            "@Gamma hello",
            1);

        GroupChatReplyCandidate candidate = Assert.Single(plan.Candidates);
        Assert.Equal(joinedAccount.Id, candidate.Speaker.Id);
        Assert.NotEqual(unjoinedAccount.Id, candidate.Speaker.Id);
        Assert.Equal(AiSpeakerSelectionStatus.MentionNotMatched, plan.SelectionStatus);
    }

    [Fact]
    public void CreatePlan_WithoutMembers_ReturnsNoCandidates()
    {
        GroupChatReplyPlanner planner = new(_database.CreateDbContextFactory());

        GroupChatReplyPlan plan = planner.CreatePlan(
            new GroupChat("Empty"),
            "hello",
            0);

        Assert.Empty(plan.Candidates);
        Assert.Equal(AiSpeakerSelectionStatus.NotAttempted, plan.SelectionStatus);
    }

    [Fact]
    public void CreatePlan_AfterPrimaryRecentlySpoke_RotatesToAnotherMember()
    {
        GroupContext context = CreateGroupContext("Alpha", "Beta");
        GroupChatReplyPlan firstPlan = context.Planner.CreatePlan(
            context.GroupChat,
            "固定话题",
            1);
        AiAccount firstSpeaker = Assert.Single(firstPlan.Candidates).Speaker;
        GroupMessageService messageService = new(context.Factory);
        bool saved = messageService.TrySaveAiReply(
            context.GroupChat,
            firstSpeaker,
            "recent reply",
            out _,
            out string errorMessage);
        Assert.True(saved, errorMessage);

        GroupChatReplyPlan nextPlan = context.Planner.CreatePlan(
            context.GroupChat,
            "固定话题",
            1);

        Assert.NotEqual(
            firstSpeaker.Id,
            Assert.Single(nextPlan.Candidates).Speaker.Id);
    }

    [Fact]
    public void CreatePlan_WithStrongRelationship_CanAddOneFollowUpSpeaker()
    {
        GroupContext context = CreateGroupContext("Alpha", "Beta");
        GroupChatReplyPlan baseline = context.Planner.CreatePlan(
            context.GroupChat,
            "hello",
            1);
        AiAccount primarySpeaker = Assert.Single(baseline.Candidates).Speaker;
        AiAccount followUpSpeaker = context.GroupChat.Members.Single(
            member => member.Id != primarySpeaker.Id);
        AiRelationshipOperationStatus updateStatus = new AiRelationshipService(
            context.Factory).TryUpdateRelationship(
                followUpSpeaker.Id,
                primarySpeaker.Id,
                100,
                100,
                100,
                out _);
        Assert.Equal(AiRelationshipOperationStatus.Success, updateStatus);

        GroupChatReplyPlan plan = context.Planner.CreatePlan(
            context.GroupChat,
            "hello",
            0.69);

        Assert.Equal(2, plan.Candidates.Count);
        Assert.Equal(primarySpeaker.Id, plan.Candidates[0].Speaker.Id);
        Assert.Equal(followUpSpeaker.Id, plan.Candidates[1].Speaker.Id);
        Assert.Equal(GroupChatReplyRole.FollowUp, plan.Candidates[1].Role);
    }

    private GroupContext CreateGroupContext(params string[] nicknames)
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccountService accountService = new(factory);
        List<AiAccount> accounts = nicknames
            .Select(nickname => CreateAccount(accountService, nickname))
            .ToList();
        GroupChat groupChat = CreateGroupChat(
            new GroupChatService(factory),
            accounts.Select(account => account.Id).ToArray());

        return new GroupContext(
            factory,
            groupChat,
            new GroupChatReplyPlanner(factory));
    }

    private static AiAccount CreateAccount(
        AiAccountService service,
        string nickname)
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

    public void Dispose()
    {
        _database.Dispose();
    }

    private sealed record GroupContext(
        VocaChatDbContextFactory Factory,
        GroupChat GroupChat,
        GroupChatReplyPlanner Planner);
}

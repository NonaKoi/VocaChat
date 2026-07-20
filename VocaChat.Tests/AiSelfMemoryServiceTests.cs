using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证个人记忆的账号隔离、持久化、用户管理和重复保护。
/// </summary>
public sealed class AiSelfMemoryServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public void UserMemory_PersistsAcrossServiceInstancesAndKeepsAccountIsolation()
    {
        AiAccount owner = CreateAccount("SelfMemoryOwner");
        AiAccount other = CreateAccount("SelfMemoryOther");
        AiSelfMemoryService service = CreateService();
        DateTime occurredAt = new(2026, 7, 20, 9, 30, 0);

        AiSelfMemoryOperationStatus createStatus =
            service.TryCreateUserMemory(
                owner.Id,
                AiSelfMemoryType.OngoingActivity,
                "  最近正在准备一次长途旅行  ",
                80,
                isUserLocked: true,
                occurredAt,
                validFrom: null,
                validUntil: null,
                out AiSelfMemory? created,
                out string createError);

        Assert.Equal(AiSelfMemoryOperationStatus.Success, createStatus);
        Assert.Equal(string.Empty, createError);
        Assert.NotNull(created);
        Assert.Equal("最近正在准备一次长途旅行", created.Summary);
        Assert.Equal(AiSelfMemorySource.User, created.Source);
        Assert.Equal(AiSelfMemoryStatus.Active, created.Status);
        Assert.True(created.IsUserLocked);
        Assert.Null(created.SourceConversationId);
        Assert.Null(created.SourceMessageId);
        Assert.Equal(occurredAt, created.OccurredAt);

        AiSelfMemoryService restartedService = CreateService();
        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            restartedService.TryGetMemories(
                owner.Id,
                100,
                status: null,
                out IReadOnlyList<AiSelfMemory> ownerMemories,
                out string queryError));
        Assert.Equal(string.Empty, queryError);
        Assert.Equal(created.Id, Assert.Single(ownerMemories).Id);

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            restartedService.TryGetMemories(
                other.Id,
                100,
                status: null,
                out IReadOnlyList<AiSelfMemory> otherMemories,
                out _));
        Assert.Empty(otherMemories);
    }

    [Fact]
    public void InvalidValuesAndMissingAccount_AreRejected()
    {
        AiAccount account = CreateAccount("SelfMemoryValidation");
        AiSelfMemoryService service = CreateService();

        AssertCreateStatus(
            AiSelfMemoryOperationStatus.InvalidSummary,
            service,
            account.Id,
            AiSelfMemoryType.PersonalFact,
            "   ",
            50);
        AssertCreateStatus(
            AiSelfMemoryOperationStatus.InvalidType,
            service,
            account.Id,
            (AiSelfMemoryType)99,
            "有效摘要",
            50);
        AssertCreateStatus(
            AiSelfMemoryOperationStatus.InvalidSalience,
            service,
            account.Id,
            AiSelfMemoryType.PersonalFact,
            "有效摘要",
            0);

        AiSelfMemoryOperationStatus invalidTimeStatus =
            service.TryCreateUserMemory(
                account.Id,
                AiSelfMemoryType.Plan,
                "下周去看展",
                60,
                isUserLocked: false,
                occurredAt: null,
                validFrom: new DateTime(2026, 7, 25),
                validUntil: new DateTime(2026, 7, 24),
                out _,
                out _);
        Assert.Equal(
            AiSelfMemoryOperationStatus.InvalidTimeRange,
            invalidTimeStatus);

        AssertCreateStatus(
            AiSelfMemoryOperationStatus.AccountNotFound,
            service,
            Guid.NewGuid(),
            AiSelfMemoryType.PersonalFact,
            "不存在账号的记忆",
            50);
        Assert.Equal(
            AiSelfMemoryOperationStatus.InvalidLimit,
            service.TryGetActiveContextMemories(
                account.Id,
                21,
                out _,
                out _));
    }

    [Fact]
    public void UpdateArchiveAndRestore_RespectOwnershipAndPersist()
    {
        AiAccount owner = CreateAccount("SelfMemoryEditOwner");
        AiAccount other = CreateAccount("SelfMemoryEditOther");
        AiSelfMemory memory = CreateMemory(
            owner,
            AiSelfMemoryType.Preference,
            "喜欢安静的咖啡馆",
            60);
        AiSelfMemoryService service = CreateService();

        Assert.Equal(
            AiSelfMemoryOperationStatus.MemoryNotFound,
            service.TryUpdateUserMemory(
                other.Id,
                memory.Id,
                AiSelfMemoryType.Preference,
                "不应修改成功",
                70,
                isUserLocked: false,
                occurredAt: null,
                validFrom: null,
                validUntil: null,
                out _,
                out _));

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryUpdateUserMemory(
                owner.Id,
                memory.Id,
                AiSelfMemoryType.Experience,
                "  上周在安静的咖啡馆完成了插画  ",
                90,
                isUserLocked: true,
                occurredAt: new DateTime(2026, 7, 13),
                validFrom: null,
                validUntil: null,
                out AiSelfMemory? updated,
                out string updateError));
        Assert.Equal(string.Empty, updateError);
        Assert.Equal("上周在安静的咖啡馆完成了插画", updated!.Summary);
        Assert.Equal(AiSelfMemorySource.User, updated.Source);

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryChangeUserManagedStatus(
                owner.Id,
                memory.Id,
                AiSelfMemoryStatus.Archived,
                out AiSelfMemory? archived,
                out _));
        Assert.Equal(AiSelfMemoryStatus.Archived, archived!.Status);

        AiSelfMemoryService restartedService = CreateService();
        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            restartedService.TryGetActiveContextMemories(
                owner.Id,
                10,
                out IReadOnlyList<AiSelfMemory> activeMemories,
                out _));
        Assert.Empty(activeMemories);

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            restartedService.TryChangeUserManagedStatus(
                owner.Id,
                memory.Id,
                AiSelfMemoryStatus.Active,
                out AiSelfMemory? restored,
                out _));
        Assert.Equal(AiSelfMemoryStatus.Active, restored!.Status);
        Assert.Equal(90, restored.Salience);
    }

    [Fact]
    public void DuplicateActiveMemory_IsRejectedCaseInsensitively()
    {
        AiAccount account = CreateAccount("SelfMemoryDuplicate");
        AiSelfMemory first = CreateMemory(
            account,
            AiSelfMemoryType.OngoingActivity,
            "Currently Painting",
            70);
        AiSelfMemoryService service = CreateService();

        AiSelfMemoryOperationStatus duplicateStatus =
            service.TryCreateUserMemory(
                account.Id,
                AiSelfMemoryType.OngoingActivity,
                "currently painting",
                95,
                isUserLocked: false,
                occurredAt: null,
                validFrom: null,
                validUntil: null,
                out AiSelfMemory? duplicate,
                out _);

        Assert.Equal(
            AiSelfMemoryOperationStatus.AlreadyExists,
            duplicateStatus);
        Assert.Equal(first.Id, duplicate!.Id);

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryChangeUserManagedStatus(
                account.Id,
                first.Id,
                AiSelfMemoryStatus.Archived,
                out _,
                out _));
        AiSelfMemory replacement = CreateMemory(
            account,
            AiSelfMemoryType.OngoingActivity,
            "currently painting",
            95);

        Assert.Equal(
            AiSelfMemoryOperationStatus.AlreadyExists,
            service.TryChangeUserManagedStatus(
                account.Id,
                first.Id,
                AiSelfMemoryStatus.Active,
                out _,
                out _));
        Assert.NotEqual(first.Id, replacement.Id);
    }

    [Fact]
    public void DirectorProposal_PersistsWithMessageProvenanceAndIsIdempotent()
    {
        AiAccount account = CreateAccount("DirectorMemory");
        (PrivateChat chat, PrivateMessage message) = CreatePersistedAiMessage(
            account,
            "最近正在准备秋季插画展");
        AiSelfMemoryService service = CreateService();
        AiSelfMemoryProposal proposal = new(
            AiSelfMemoryProposalOperation.Add,
            TargetMemoryId: null,
            AiSelfMemoryType.OngoingActivity,
            "正在准备秋季插画展",
            "这是当前发言者明确表达的持续事项");

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryValidateDirectorProposals(
                account.Id,
                new[] { proposal },
                out AiSelfMemoryProposalValidationResult validation,
                out string validationError));
        Assert.Equal(string.Empty, validationError);
        Assert.Equal(proposal, Assert.Single(validation.AcceptedProposals));

        AiPersistedMessageEvidence evidence = new(
            message.Id,
            message.Content,
            message.SentAt);
        AiSelfMemoryProposalApplicationResult firstResult =
            service.ApplyDirectorProposals(
                account.Id,
                chat.Id,
                validation.AcceptedProposals,
                new[] { evidence });
        AiSelfMemoryProposalApplicationResult retryResult =
            service.ApplyDirectorProposals(
                account.Id,
                chat.Id,
                validation.AcceptedProposals,
                new[] { evidence });

        Assert.Equal(AiSelfMemoryProposalApplicationStatus.Success, firstResult.Status);
        Assert.Equal(1, firstResult.AppliedCount);
        Assert.Equal(1, retryResult.AlreadyAppliedCount);
        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            CreateService().TryGetActiveContextMemories(
                account.Id,
                10,
                out IReadOnlyList<AiSelfMemory> memories,
                out _));
        AiSelfMemory stored = Assert.Single(memories);
        Assert.Equal(AiSelfMemorySource.Director, stored.Source);
        Assert.Equal(chat.Id, stored.SourceConversationId);
        Assert.Equal(message.Id, stored.SourceMessageId);
    }

    [Fact]
    public void DirectorProposal_CannotModifyUserOrLockedMemory()
    {
        AiAccount account = CreateAccount("ProtectedMemory");
        AiSelfMemory userMemory = CreateMemory(
            account,
            AiSelfMemoryType.Preference,
            "喜欢安静的展览",
            80);
        AiSelfMemoryService service = CreateService();
        AiSelfMemoryProposal updateUserMemory = new(
            AiSelfMemoryProposalOperation.Update,
            userMemory.Id,
            AiSelfMemoryType.Preference,
            "现在只喜欢热闹的展览",
            "试图覆盖用户记忆");

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryValidateDirectorProposals(
                account.Id,
                new[] { updateUserMemory },
                out AiSelfMemoryProposalValidationResult validation,
                out _));
        AiSelfMemoryProposalDecision decision = Assert.Single(
            validation.Decisions);
        Assert.False(decision.IsAccepted);
        Assert.Contains("用户来源", decision.Reason);

        AiSelfMemoryProposal personalFact = new(
            AiSelfMemoryProposalOperation.Add,
            null,
            AiSelfMemoryType.PersonalFact,
            "突然更换了职业",
            "稳定资料不应自动修改");
        service.TryValidateDirectorProposals(
            account.Id,
            new[] { personalFact },
            out validation,
            out _);
        Assert.False(Assert.Single(validation.Decisions).IsAccepted);
    }

    [Fact]
    public void DirectorUpdate_SupersedesOldMemoryAndRejectsOtherAccountEvidence()
    {
        AiAccount owner = CreateAccount("DirectorUpdateOwner");
        AiAccount other = CreateAccount("DirectorUpdateOther");
        (PrivateChat firstChat, PrivateMessage firstMessage) =
            CreatePersistedAiMessage(owner, "正在准备秋季插画展");
        AiSelfMemoryProposal add = new(
            AiSelfMemoryProposalOperation.Add,
            null,
            AiSelfMemoryType.OngoingActivity,
            "正在准备秋季插画展",
            "持续事项");
        AiSelfMemoryService service = CreateService();
        service.ApplyDirectorProposals(
            owner.Id,
            firstChat.Id,
            new[] { add },
            new[]
            {
                new AiPersistedMessageEvidence(
                    firstMessage.Id,
                    firstMessage.Content,
                    firstMessage.SentAt)
            });
        service.TryGetActiveContextMemories(
            owner.Id,
            10,
            out IReadOnlyList<AiSelfMemory> current,
            out _);
        AiSelfMemory oldMemory = Assert.Single(current);

        (PrivateChat secondChat, PrivateMessage secondMessage) =
            CreatePersistedAiMessage(owner, "已经完成秋季插画展的准备");
        AiSelfMemoryProposal update = new(
            AiSelfMemoryProposalOperation.Update,
            oldMemory.Id,
            AiSelfMemoryType.Experience,
            "已经完成秋季插画展的准备",
            "持续事项已经完成");
        service.TryValidateDirectorProposals(
            owner.Id,
            new[] { update },
            out AiSelfMemoryProposalValidationResult validation,
            out _);
        AiSelfMemoryProposalApplicationResult result =
            service.ApplyDirectorProposals(
                owner.Id,
                secondChat.Id,
                validation.AcceptedProposals,
                new[]
                {
                    new AiPersistedMessageEvidence(
                        secondMessage.Id,
                        secondMessage.Content,
                        secondMessage.SentAt)
                });
        Assert.Equal(1, result.AppliedCount);

        service.TryGetMemories(
            owner.Id,
            10,
            status: null,
            out IReadOnlyList<AiSelfMemory> allMemories,
            out _);
        Assert.Contains(allMemories, memory =>
            memory.Id == oldMemory.Id
            && memory.Status == AiSelfMemoryStatus.Superseded);
        Assert.Contains(allMemories, memory =>
            memory.Type == AiSelfMemoryType.Experience
            && memory.Status == AiSelfMemoryStatus.Active);

        AiSelfMemoryProposalApplicationResult crossAccountResult =
            service.ApplyDirectorProposals(
                other.Id,
                secondChat.Id,
                new[] { add },
                new[]
                {
                    new AiPersistedMessageEvidence(
                        secondMessage.Id,
                        secondMessage.Content,
                        secondMessage.SentAt)
                });
        Assert.Equal(1, crossAccountResult.RejectedCount);
    }

    private AiSelfMemory CreateMemory(
        AiAccount account,
        AiSelfMemoryType type,
        string summary,
        int salience)
    {
        AiSelfMemoryOperationStatus status =
            CreateService().TryCreateUserMemory(
                account.Id,
                type,
                summary,
                salience,
                isUserLocked: false,
                occurredAt: null,
                validFrom: null,
                validUntil: null,
                out AiSelfMemory? memory,
                out string errorMessage);
        Assert.Equal(AiSelfMemoryOperationStatus.Success, status);
        Assert.Equal(string.Empty, errorMessage);
        return Assert.IsType<AiSelfMemory>(memory);
    }

    private static void AssertCreateStatus(
        AiSelfMemoryOperationStatus expectedStatus,
        AiSelfMemoryService service,
        Guid aiAccountId,
        AiSelfMemoryType type,
        string summary,
        int salience)
    {
        Assert.Equal(
            expectedStatus,
            service.TryCreateUserMemory(
                aiAccountId,
                type,
                summary,
                salience,
                isUserLocked: false,
                occurredAt: null,
                validFrom: null,
                validUntil: null,
                out _,
                out _));
    }

    private AiAccount CreateAccount(string nickname)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            $"{nickname}-{Guid.NewGuid().ToString("N")[..8]}",
            string.Empty,
            string.Empty,
            string.Empty,
            out AiAccount? account,
            out string errorMessage), errorMessage);
        return Assert.IsType<AiAccount>(account);
    }

    private (PrivateChat Chat, PrivateMessage Message) CreatePersistedAiMessage(
        AiAccount account,
        string content)
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        Contact contact = Assert.IsType<Contact>(
            new ContactService(factory).FindByAiAccountId(account.Id));
        PrivateChatService chatService = new(factory);
        Assert.True(chatService.TryGetOrCreate(
            contact.Id,
            out PrivateChat? chat,
            out _,
            out string chatError), chatError);
        Assert.True(chatService.TrySaveAiReply(
            chat!,
            account,
            content,
            out PrivateMessage? message,
            out string messageError), messageError);
        return (
            Assert.IsType<PrivateChat>(chat),
            Assert.IsType<PrivateMessage>(message));
    }

    private AiSelfMemoryService CreateService()
    {
        return new AiSelfMemoryService(_database.CreateDbContextFactory());
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

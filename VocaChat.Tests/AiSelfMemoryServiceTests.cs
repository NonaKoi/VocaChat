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
        Assert.Equal(memory.Id, updated.SupersedesMemoryId);
        Assert.NotEqual(memory.Id, updated.Id);

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryChangeUserManagedStatus(
                owner.Id,
                updated.Id,
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
                updated.Id,
                AiSelfMemoryStatus.Active,
                out AiSelfMemory? restored,
                out _));
        Assert.Equal(AiSelfMemoryStatus.Active, restored!.Status);
        Assert.Equal(90, restored.Salience);

        restartedService.TryGetMemories(
            owner.Id,
            10,
            status: null,
            out IReadOnlyList<AiSelfMemory> history,
            out _);
        Assert.Contains(history, item =>
            item.Id == memory.Id
            && item.Status == AiSelfMemoryStatus.Superseded);
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
        AiSelfMemoryProposal proposal = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.OngoingActivity,
            "ongoing:autumn-art-exhibition",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Evolving,
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
                CreateJudgment(validation.AcceptedProposals),
                new[] { evidence });
        AiSelfMemoryProposalApplicationResult retryResult =
            service.ApplyDirectorProposals(
                account.Id,
                chat.Id,
                validation.AcceptedProposals,
                CreateJudgment(validation.AcceptedProposals),
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
        AiSelfMemoryProposal updateUserMemory = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Update,
            AiSelfMemoryType.Preference,
            userMemory.FactKey,
            AiSelfMemoryFactNature.Subjective,
            AiSelfMemoryMutability.Mutable,
            "现在只喜欢热闹的展览",
            "试图覆盖用户记忆",
            userMemory.Id);

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

        AiSelfMemoryProposal personalFact = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.PersonalFact,
            "profile:occupation",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Mutable,
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
    public void ApplyDirectorProposals_RechecksHardRulesAfterSemanticAcceptance()
    {
        AiAccount account = CreateAccount("FinalHardRule");
        (PrivateChat chat, PrivateMessage message) =
            CreatePersistedAiMessage(account, "我突然换了职业");
        AiSelfMemoryProposal proposal = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.PersonalFact,
            "profile:occupation",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Mutable,
            "我突然换了职业",
            "尝试通过语义判断写入稳定资料");
        AiSelfMemoryService service = CreateService();

        AiSelfMemoryProposalApplicationResult result =
            service.ApplyDirectorProposals(
                account.Id,
                chat.Id,
                new[] { proposal },
                CreateJudgment(new[] { proposal }),
                new[]
                {
                    new AiPersistedMessageEvidence(
                        message.Id,
                        message.Content,
                        message.SentAt)
                });

        Assert.Equal(
            AiSelfMemoryProposalApplicationStatus.PartialFailure,
            result.Status);
        Assert.Equal(1, result.RejectedCount);
        service.TryGetMemories(
            account.Id,
            10,
            status: null,
            out IReadOnlyList<AiSelfMemory> memories,
            out _);
        Assert.Empty(memories);
    }

    [Fact]
    public void ApplyDirectorProposals_PendingDecisionDoesNotPersistMemory()
    {
        AiAccount account = CreateAccount("PendingMemory");
        (PrivateChat chat, PrivateMessage message) =
            CreatePersistedAiMessage(account, "也许最近会去学陶艺");
        AiSelfMemoryProposal proposal = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.Plan,
            "plan:learn-pottery",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Evolving,
            "最近可能会去学陶艺",
            "证据仍然含混");
        AiSelfMemoryService service = CreateService();

        AiSelfMemoryProposalApplicationResult result =
            service.ApplyDirectorProposals(
                account.Id,
                chat.Id,
                new[] { proposal },
                AiSelfMemorySemanticJudgmentResult.Pending(
                    new[] { proposal },
                    "证据不足"),
                new[]
                {
                    new AiPersistedMessageEvidence(
                        message.Id,
                        message.Content,
                        message.SentAt)
                });

        Assert.Equal(
            AiSelfMemoryProposalApplicationStatus.PartialFailure,
            result.Status);
        Assert.Equal(1, result.PendingCount);
        service.TryGetMemories(
            account.Id,
            10,
            status: null,
            out IReadOnlyList<AiSelfMemory> memories,
            out _);
        Assert.Empty(memories);
    }

    [Fact]
    public void DirectorProposal_CannotModifyImmutableDirectorMemory()
    {
        AiAccount account = CreateAccount("ImmutableDirectorMemory");
        (PrivateChat chat, PrivateMessage message) =
            CreatePersistedAiMessage(account, "上周在海边看到了流星");
        AiSelfMemoryService service = CreateService();
        AiSelfMemoryProposal add = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.Experience,
            "experience:seaside-meteor",
            AiSelfMemoryFactNature.Narrative,
            AiSelfMemoryMutability.Immutable,
            "上周在海边看到了流星",
            "已经发生的经历");
        service.ApplyDirectorProposals(
            account.Id,
            chat.Id,
            new[] { add },
            CreateJudgment(new[] { add }),
            new[]
            {
                new AiPersistedMessageEvidence(
                    message.Id,
                    message.Content,
                    message.SentAt)
            });
        service.TryGetActiveContextMemories(
            account.Id,
            10,
            out IReadOnlyList<AiSelfMemory> memories,
            out _);
        AiSelfMemory immutable = Assert.Single(memories);
        Assert.Equal(AiSelfMemoryMutability.Immutable, immutable.Mutability);

        AiSelfMemoryProposal update = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Update,
            AiSelfMemoryType.Experience,
            immutable.FactKey,
            AiSelfMemoryFactNature.Narrative,
            AiSelfMemoryMutability.Immutable,
            "上周没有去过海边",
            "试图改写已发生经历",
            immutable.Id);
        service.TryValidateDirectorProposals(
            account.Id,
            new[] { update },
            out AiSelfMemoryProposalValidationResult validation,
            out _);

        AiSelfMemoryProposalDecision decision =
            Assert.Single(validation.Decisions);
        Assert.False(decision.IsAccepted);
        Assert.Contains("恒定", decision.Reason);
    }

    [Fact]
    public void SameFactKey_CanExistInDifferentWorlds_AndContextUsesCurrentWorld()
    {
        AiAccount account = CreateAccount("ScopedMemory");
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        CharacterWorldService worldService = new(factory);
        Assert.Equal(
            CharacterWorldOperationStatus.Success,
            worldService.TryCreate(
                "镜海世界",
                "一座漂浮群岛构成的幻想世界。",
                out CharacterWorld? otherWorld,
                out string worldError));
        Assert.Equal(string.Empty, worldError);
        AiSelfMemoryService service = new(factory);

        AiSelfMemoryWriteData defaultWorldMemory = new(
            AiSelfMemoryType.PersonalFact,
            "出生于宁波",
            90,
            IsUserLocked: true,
            OccurredAt: null,
            ValidFrom: null,
            ValidUntil: null,
            FactKey: "birthplace",
            FactNature: AiSelfMemoryFactNature.Objective,
            Mutability: AiSelfMemoryMutability.Immutable,
            CharacterWorldId: CharacterWorld.DefaultWorldId);
        AiSelfMemoryWriteData otherWorldMemory = defaultWorldMemory with
        {
            Summary = "出生于镜海港",
            CharacterWorldId = otherWorld!.Id
        };

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryCreateUserMemory(
                account.Id,
                defaultWorldMemory,
                out AiSelfMemory? currentWorldFact,
                out _));
        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryCreateUserMemory(
                account.Id,
                otherWorldMemory,
                out AiSelfMemory? otherWorldFact,
                out _));
        Assert.NotEqual(currentWorldFact!.Id, otherWorldFact!.Id);

        service.TryGetActiveContextMemories(
            account.Id,
            10,
            out IReadOnlyList<AiSelfMemory> recalled,
            out _);
        AiSelfMemory recalledFact = Assert.Single(recalled);
        Assert.Equal(CharacterWorld.DefaultWorldId, recalledFact.CharacterWorldId);
        Assert.Equal("出生于宁波", recalledFact.Summary);
    }

    [Fact]
    public void DirectorProposal_CannotPersistUnverifiedExternalStatus()
    {
        AiAccount account = CreateAccount("ExternalStatusMemory");
        AiSelfMemoryService service = CreateService();
        AiSelfMemoryProposal proposal = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.Experience,
            "external:teahouse-opening-hours",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Mutable,
            "水巷茶室今晚通宵开放",
            "将外部营业信息当成个人记忆");

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryValidateDirectorProposals(
                account.Id,
                new[] { proposal },
                out AiSelfMemoryProposalValidationResult validation,
                out string errorMessage));

        Assert.Equal(string.Empty, errorMessage);
        Assert.Empty(validation.AcceptedProposals);
        AiSelfMemoryProposalDecision decision = Assert.Single(
            validation.Decisions);
        Assert.False(decision.IsAccepted);
        Assert.Contains("营业、活动或经营信息", decision.Reason);
    }

    [Fact]
    public void DirectorProposal_CustomWorldNarrative_CanBecomeNarrativeCandidate()
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        CharacterWorldService worldService = new(factory);
        Assert.Equal(
            CharacterWorldOperationStatus.Success,
            worldService.TryCreate(
                "镜海群岛",
                "浮空岛之间以潮汐列车往来，夜间会举办潮汐歌会。",
                out CharacterWorld? world,
                out string worldError));
        Assert.Equal(string.Empty, worldError);
        AiAccount account = CreateAccount(
            "NarrativeCandidate",
            world!.Id);
        AiSelfMemoryService service = CreateService();
        AiSelfMemoryProposal proposal = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.Experience,
            "experience:tide-song-concert",
            AiSelfMemoryFactNature.Narrative,
            AiSelfMemoryMutability.Immutable,
            "今晚在星渊茶室听过潮汐歌会",
            "保存当前世界中的本人经历");

        Assert.Equal(
            AiSelfMemoryOperationStatus.Success,
            service.TryValidateDirectorProposals(
                account.Id,
                new[] { proposal },
                out AiSelfMemoryProposalValidationResult validation,
                out string validationError));
        Assert.Equal(string.Empty, validationError);
        Assert.Single(validation.AcceptedProposals);

        (PrivateChat chat, PrivateMessage message) =
            CreatePersistedAiMessage(
                account,
                "今晚在星渊茶室听过潮汐歌会");
        AiSelfMemoryProposalApplicationResult result =
            service.ApplyDirectorProposals(
                account.Id,
                chat.Id,
                validation.AcceptedProposals,
                CreateJudgment(validation.AcceptedProposals),
                new[]
                {
                    new AiPersistedMessageEvidence(
                        message.Id,
                        message.Content,
                        message.SentAt)
                });

        Assert.Equal(
            AiSelfMemoryProposalApplicationStatus.Success,
            result.Status);
        service.TryGetActiveContextMemories(
            account.Id,
            10,
            out IReadOnlyList<AiSelfMemory> memories,
            out _);
        AiSelfMemory memory = Assert.Single(memories);
        Assert.Equal(world.Id, memory.CharacterWorldId);
        Assert.Equal(
            AiSelfMemoryTrustLevel.NarrativeCandidate,
            memory.TrustLevel);
        Assert.Equal(AiSelfMemoryFactNature.Narrative, memory.FactNature);
    }

    [Fact]
    public void DirectorProposal_CannotUpdateMemoryFromAccountsPreviousWorld()
    {
        VocaChat.Data.VocaChatDbContextFactory factory =
            _database.CreateDbContextFactory();
        CharacterWorldService worldService = new(factory);
        worldService.TryCreate(
            "旧世界",
            "角色曾经使用的世界设定。",
            out CharacterWorld? oldWorld,
            out _);
        AiAccount account = CreateAccount("WorldSwitch", oldWorld!.Id);
        (PrivateChat chat, PrivateMessage message) =
            CreatePersistedAiMessage(account, "在旧世界记录过潮汐列车");
        AiSelfMemoryService service = CreateService();
        AiSelfMemoryProposal add = CreateProposal(
            account,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.Experience,
            "experience:tide-train",
            AiSelfMemoryFactNature.Narrative,
            AiSelfMemoryMutability.Mutable,
            "在旧世界记录过潮汐列车",
            "保存旧世界经历");
        AiSelfMemoryProposalApplicationResult addResult =
            service.ApplyDirectorProposals(
                account.Id,
                chat.Id,
                new[] { add },
                CreateJudgment(new[] { add }),
                new[]
                {
                    new AiPersistedMessageEvidence(
                        message.Id,
                        message.Content,
                        message.SentAt)
                });
        Assert.Equal(
            AiSelfMemoryProposalApplicationStatus.Success,
            addResult.Status);
        service.TryGetActiveContextMemories(
            account.Id,
            10,
            out IReadOnlyList<AiSelfMemory> oldWorldMemories,
            out _);
        AiSelfMemory oldMemory = Assert.Single(oldWorldMemories);

        AiAccountUpdateStatus updateStatus =
            new AiAccountService(factory).TryUpdateAiAccount(
                account.Id,
                new AiAccountUpdateData
                {
                    Nickname = account.Nickname,
                    VcNumber = account.VcNumber,
                    IdentityDescription = account.IdentityDescription,
                    Personality = account.Personality,
                    SpeakingStyle = account.SpeakingStyle,
                    Signature = account.Signature,
                    Birthday = account.Birthday,
                    Gender = account.Gender,
                    Location = account.Location,
                    Occupation = account.Occupation,
                    Hometown = account.Hometown,
                    OnlineStatus = account.OnlineStatus,
                    CharacterWorldId = CharacterWorld.DefaultWorldId,
                    InterestTags = account.Tags
                        .Where(tag => tag.Type == AiAccountTagType.Interest)
                        .Select(tag => tag.Value)
                        .ToArray(),
                    PersonalityTags = account.Tags
                        .Where(tag =>
                            tag.Type == AiAccountTagType.Personality)
                        .Select(tag => tag.Value)
                        .ToArray()
                },
                out _,
                out string updateError);
        Assert.Equal(AiAccountUpdateStatus.Success, updateStatus);
        Assert.Equal(string.Empty, updateError);

        AiSelfMemoryProposal update = new(
            AiSelfMemoryProposalOperation.Update,
            oldMemory.Id,
            account.Id,
            CharacterWorld.DefaultWorldId,
            AiSelfMemoryType.Experience,
            oldMemory.FactKey,
            AiSelfMemoryFactNature.Narrative,
            AiSelfMemoryMutability.Mutable,
            "在现实世界乘过潮汐列车",
            "尝试跨世界修改旧记忆");
        service.TryValidateDirectorProposals(
            account.Id,
            new[] { update },
            out AiSelfMemoryProposalValidationResult validation,
            out _);

        AiSelfMemoryProposalDecision decision =
            Assert.Single(validation.Decisions);
        Assert.False(decision.IsAccepted);
        Assert.Contains("其他角色世界", decision.Reason);
    }

    [Fact]
    public void DirectorUpdate_SupersedesOldMemoryAndRejectsOtherAccountEvidence()
    {
        AiAccount owner = CreateAccount("DirectorUpdateOwner");
        AiAccount other = CreateAccount("DirectorUpdateOther");
        (PrivateChat firstChat, PrivateMessage firstMessage) =
            CreatePersistedAiMessage(owner, "正在准备秋季插画展");
        AiSelfMemoryProposal add = CreateProposal(
            owner,
            AiSelfMemoryProposalOperation.Add,
            AiSelfMemoryType.OngoingActivity,
            "ongoing:autumn-art-exhibition",
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Evolving,
            "正在准备秋季插画展",
            "持续事项");
        AiSelfMemoryService service = CreateService();
        service.ApplyDirectorProposals(
            owner.Id,
            firstChat.Id,
            new[] { add },
            CreateJudgment(new[] { add }),
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
        AiSelfMemoryProposal update = CreateProposal(
            owner,
            AiSelfMemoryProposalOperation.Update,
            AiSelfMemoryType.Experience,
            oldMemory.FactKey,
            AiSelfMemoryFactNature.Objective,
            AiSelfMemoryMutability.Evolving,
            "已经完成秋季插画展的准备",
            "持续事项已经完成",
            oldMemory.Id);
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
                CreateJudgment(validation.AcceptedProposals),
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
                CreateJudgment(new[] { add }),
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

    private AiAccount CreateAccount(
        string nickname,
        Guid? characterWorldId = null)
    {
        AiAccountService service = new(_database.CreateDbContextFactory());
        Assert.True(service.TryCreateAiAccount(
            new AiAccountCreationData
            {
                Nickname = $"{nickname}-{Guid.NewGuid().ToString("N")[..8]}",
                CharacterWorldId = characterWorldId
            },
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

    private static AiSelfMemoryProposal CreateProposal(
        AiAccount account,
        AiSelfMemoryProposalOperation operation,
        AiSelfMemoryType type,
        string factKey,
        AiSelfMemoryFactNature factNature,
        AiSelfMemoryMutability mutability,
        string summary,
        string reason,
        Guid? targetMemoryId = null)
    {
        return new AiSelfMemoryProposal(
            operation,
            targetMemoryId,
            account.Id,
            account.CharacterWorldId,
            type,
            factKey,
            factNature,
            mutability,
            summary,
            reason);
    }

    private static AiSelfMemorySemanticJudgmentResult CreateJudgment(
        IReadOnlyList<AiSelfMemoryProposal> proposals)
    {
        IReadOnlyList<AiSelfMemorySemanticDecision> decisions = proposals
            .Select((proposal, index) => new AiSelfMemorySemanticDecision(
                index,
                proposal.Operation switch
                {
                    AiSelfMemoryProposalOperation.Add
                        => AiSelfMemorySemanticOutcome.Accept,
                    AiSelfMemoryProposalOperation.Update
                        => AiSelfMemorySemanticOutcome.Supersede,
                    AiSelfMemoryProposalOperation.Archive
                        => AiSelfMemorySemanticOutcome.Archive,
                    _ => AiSelfMemorySemanticOutcome.Reject
                },
                proposal.TargetMemoryId,
                proposal.FactKey,
                proposal.FactNature,
                proposal.Mutability,
                "测试语义判断"))
            .ToList()
            .AsReadOnly();

        return new AiSelfMemorySemanticJudgmentResult(
            decisions,
            UsedFallback: false,
            FallbackReason: string.Empty);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}

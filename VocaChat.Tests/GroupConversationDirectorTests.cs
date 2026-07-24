using System.Net;
using System.Text;
using System.Text.Json;
using VocaChat.Data;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证群级导演只在当前群成员与消息边界内分配发言者和语义职责。
/// </summary>
public sealed class GroupConversationDirectorTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task RuleDirector_WithMentions_CreatesValidDistinctSpeakerPlans()
    {
        GroupContext context = CreateContext("Alpha", "Beta", "Gamma");
        GroupConversationPlanningRequest request = CreateRequest(
            context,
            "请 @Beta 先说，@Alpha 和 @Gamma 再补充");
        RuleBasedGroupConversationDirector director = new(context.Planner);

        GroupConversationTurnPlan plan = await director.CreatePlanAsync(request);

        Assert.True(new GroupConversationPlanValidator().TryValidate(
            request,
            plan,
            out string errorMessage), errorMessage);
        Assert.True(plan.UsedRuleFallback);
        Assert.Equal(AiSpeakerSelectionStatus.MentionMatched, plan.SelectionStatus);
        Assert.Equal(
            new[] { "Beta", "Alpha", "Gamma" },
            plan.Speakers.Select(speakerPlan => context.GroupChat.Members
                .Single(member =>
                    member.Id == speakerPlan.SpeakerAiAccountId)
                .Nickname));
        Assert.Equal(
            plan.Speakers.Count,
            plan.Speakers.Select(item => item.NewContribution)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
    }

    [Fact]
    public async Task ModelDirector_WithValidPlan_PreservesExplicitAudienceAndRoles()
    {
        GroupContext context = CreateContext("Alpha", "Beta");
        GroupConversationPlanningRequest request = CreateRequest(
            context,
            "你们怎么看这个安排？");
        AiAccount first = context.GroupChat.Members[0];
        AiAccount second = context.GroupChat.Members[1];
        RecordingHandler handler = new(CreateResponse(CreatePlanJson(
            request.AnchorMessage!.Id,
            first.Id,
            second.Id)));
        OpenAiCompatibleGroupConversationDirector director = CreateDirector(
            context.Planner,
            handler);

        GroupConversationTurnPlan plan = await director.CreatePlanAsync(request);

        Assert.False(plan.UsedRuleFallback);
        Assert.Equal(2, plan.Speakers.Count);
        Assert.Null(plan.ModelPlanRejectionReason);
        Assert.Equal(GroupConversationAudience.LocalUser, plan.Speakers[0].Audience);
        Assert.Equal(GroupConversationRole.DirectAnswer, plan.Speakers[0].Role);
        Assert.Equal(
            GroupConversationAudience.SpecificAiAccount,
            plan.Speakers[1].Audience);
        Assert.Equal(first.Id, plan.Speakers[1].TargetAiAccountId);
        Assert.Equal(GroupConversationRole.Disagree, plan.Speakers[1].Role);
        Assert.NotNull(handler.RequestBody);
        using JsonDocument requestBody = JsonDocument.Parse(
            handler.RequestBody!);
        Assert.Equal(
            768,
            requestBody.RootElement.GetProperty("max_tokens").GetInt32());
        string userPrompt = requestBody.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("最多发言者：3", userPrompt);
        Assert.Contains($"{first.Nickname}本人的相关个人记忆", userPrompt);
        Assert.Contains("→", userPrompt);
        Assert.Contains("与潜在回应对象的方向上下文", userPrompt);
        Assert.Contains($"{first.Nickname}所属角色世界：现实世界", userPrompt);
        Assert.Contains("角色世界权威说明", userPrompt);
        Assert.Contains("本人可用的群聊世界认知", userPrompt);
        Assert.Contains("不可转给其他候选人", userPrompt);
        Assert.Contains("应优先于只有宽泛职业或兴趣关联的候选人", userPrompt);
        Assert.Contains("不能并列重复同一结论和理由", userPrompt);
        Assert.Contains("不能据此虚构双方过去共同做过的事", userPrompt);
        Assert.Contains("不得与已有记忆冲突", userPrompt);

        string systemPrompt = requestBody.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("优先选择该候选人", systemPrompt);
        Assert.Contains("后续成员必须回应更早发言者", systemPrompt);
        Assert.Contains("不要写成带有大量具体细节的台词草稿", systemPrompt);
        Assert.Contains("不能从宽泛资料推导出具体地点", systemPrompt);
        Assert.Contains("允许角色在自己的角色世界中自然引入", systemPrompt);
        Assert.Contains("默认现实世界中没有用户或确认资料来源", systemPrompt);
        Assert.Contains("不证明双方存在未记录的共同经历", systemPrompt);
        Assert.Contains("不得把推测写成个人事实", systemPrompt);
    }

    [Fact]
    public async Task ModelDirector_WithMissingSafeFieldsAndInvalidMessageTarget_UsesDefaults()
    {
        GroupContext context = CreateContext("Alpha");
        GroupConversationPlanningRequest request = CreateRequest(
            context,
            "聊聊今天适合做什么");
        AiAccount speaker = context.GroupChat.Members[0];
        string json = JsonSerializer.Serialize(new
        {
            speakers = new[]
            {
                new
                {
                    speakerAiAccountId = speaker.Id,
                    replyTargetMessageId = Guid.NewGuid(),
                    targetAiAccountId = (Guid?)null,
                    audience = "LocalUser",
                    role = "DirectAnswer",
                    newContribution = "提出一种适合今天的活动"
                }
            }
        });
        OpenAiCompatibleGroupConversationDirector director = CreateDirector(
            context.Planner,
            new RecordingHandler(CreateResponse(json)));

        GroupConversationTurnPlan plan = await director.CreatePlanAsync(request);

        Assert.False(plan.UsedRuleFallback);
        Assert.Equal(request.AnchorMessage!.Content, plan.TopicFocus);
        Assert.False(string.IsNullOrWhiteSpace(plan.TurnGoal));
        Assert.Empty(plan.CoveredPoints);
        Assert.Empty(plan.UnresolvedGoals);
        GroupConversationSpeakerPlan speakerPlan = Assert.Single(plan.Speakers);
        Assert.Equal(request.AnchorMessage.Id, speakerPlan.ReplyTargetMessageId);
        Assert.False(string.IsNullOrWhiteSpace(speakerPlan.ResponseGoal));
        Assert.Empty(speakerPlan.AvoidedRepetition);
    }

    [Fact]
    public async Task ModelDirector_WhenSelectingNonMember_UsesRuleFallback()
    {
        GroupContext context = CreateContext("Alpha");
        GroupConversationPlanningRequest request = CreateRequest(
            context,
            "今天做什么？");
        string invalidJson = JsonSerializer.Serialize(new
        {
            topicFocus = "今天的安排",
            turnGoal = "回答用户",
            coveredPoints = Array.Empty<string>(),
            unresolvedGoals = new[] { "给出安排" },
            speakers = new[]
            {
                new
                {
                    speakerAiAccountId = Guid.NewGuid(),
                    replyTargetMessageId = request.AnchorMessage!.Id,
                    targetAiAccountId = (Guid?)null,
                    audience = "LocalUser",
                    role = "DirectAnswer",
                    responseGoal = "回答用户",
                    newContribution = "给出一个安排",
                    avoidedRepetition = Array.Empty<string>()
                }
            }
        });
        OpenAiCompatibleGroupConversationDirector director = CreateDirector(
            context.Planner,
            new RecordingHandler(CreateResponse(invalidJson)));

        GroupConversationTurnPlan plan = await director.CreatePlanAsync(request);

        Assert.True(plan.UsedRuleFallback);
        Assert.False(string.IsNullOrWhiteSpace(
            plan.ModelPlanRejectionReason));
        GroupConversationSpeakerPlan speakerPlan = Assert.Single(plan.Speakers);
        Assert.Equal(
            context.GroupChat.Members[0].Id,
            speakerPlan.SpeakerAiAccountId);
    }

    [Fact]
    public async Task ModelDirector_WhenIgnoringMention_UsesMentionedMemberFallback()
    {
        GroupContext context = CreateContext("Alpha", "Beta");
        GroupConversationPlanningRequest request = CreateRequest(
            context,
            "@Beta 你怎么看？");
        AiAccount alpha = context.GroupChat.Members.Single(member =>
            member.Nickname == "Alpha");
        AiAccount beta = context.GroupChat.Members.Single(member =>
            member.Nickname == "Beta");
        string invalidJson = JsonSerializer.Serialize(new
        {
            topicFocus = "当前问题",
            turnGoal = "回应用户",
            coveredPoints = Array.Empty<string>(),
            unresolvedGoals = new[] { "给出判断" },
            speakers = new[]
            {
                new
                {
                    speakerAiAccountId = alpha.Id,
                    replyTargetMessageId = request.AnchorMessage!.Id,
                    targetAiAccountId = (Guid?)null,
                    audience = "LocalUser",
                    role = "DirectAnswer",
                    responseGoal = "回应用户",
                    newContribution = "给出当前判断",
                    avoidedRepetition = Array.Empty<string>()
                }
            }
        });
        OpenAiCompatibleGroupConversationDirector director = CreateDirector(
            context.Planner,
            new RecordingHandler(CreateResponse(invalidJson)));

        GroupConversationTurnPlan plan = await director.CreatePlanAsync(request);

        Assert.True(plan.UsedRuleFallback);
        Assert.False(string.IsNullOrWhiteSpace(
            plan.ModelPlanRejectionReason));
        Assert.Equal(
            beta.Id,
            Assert.Single(plan.Speakers).SpeakerAiAccountId);
    }

    [Fact]
    public async Task ModelDirector_WhenPlanBorrowsOtherCandidatesWorldFact_UsesFallback()
    {
        GroupContext context = CreateContext("Alpha", "Beta");
        AiAccount alpha = context.GroupChat.Members.Single(member =>
            member.Nickname == "Alpha");
        AiAccount beta = context.GroupChat.Members.Single(member =>
            member.Nickname == "Beta");
        CharacterWorld alphaWorld = new(
            "星环帝国",
            "由星环舰队维持航路的世界。");
        alpha.AssignCharacterWorld(alphaWorld);
        GroupConversationPlanningRequest request = CreateRequest(
            context,
            "你们怎么看今天的安排？");
        string invalidJson = JsonSerializer.Serialize(new
        {
            topicFocus = "今天的安排",
            turnGoal = "回答用户",
            coveredPoints = Array.Empty<string>(),
            unresolvedGoals = new[] { "给出判断" },
            speakers = new[]
            {
                new
                {
                    speakerAiAccountId = beta.Id,
                    replyTargetMessageId = request.AnchorMessage!.Id,
                    targetAiAccountId = (Guid?)null,
                    audience = "LocalUser",
                    role = "DirectAnswer",
                    responseGoal = "回答用户",
                    newContribution = "引用星环帝国舰队的经验给出建议",
                    avoidedRepetition = Array.Empty<string>()
                }
            }
        });
        OpenAiCompatibleGroupConversationDirector director = CreateDirector(
            context.Planner,
            new RecordingHandler(CreateResponse(invalidJson)));

        GroupConversationTurnPlan plan = await director.CreatePlanAsync(
            request);

        Assert.True(plan.UsedRuleFallback);
        Assert.Contains(
            "世界认知边界",
            plan.ModelPlanRejectionReason,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            plan.Speakers,
            item => item.SpeakerAiAccountId == beta.Id
                && item.NewContribution.Contains(
                    "星环帝国",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_WhenSpeakerIsRepeated_RejectsPlan()
    {
        GroupContext context = CreateContext("Alpha", "Beta");
        GroupConversationPlanningRequest request = CreateRequest(
            context,
            "聊聊周末");
        Guid repeatedId = context.GroupChat.Members[0].Id;
        GroupConversationTurnPlan plan = new()
        {
            AnchorMessageId = request.AnchorMessage!.Id,
            TopicFocus = "周末安排",
            TurnGoal = "回答用户",
            Speakers = new GroupConversationSpeakerPlan[]
            {
                CreateSpeakerPlan(repeatedId, request.AnchorMessage.Id, "角度一"),
                CreateSpeakerPlan(repeatedId, request.AnchorMessage.Id, "角度二")
            },
            SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection
        };

        bool valid = new GroupConversationPlanValidator().TryValidate(
            request,
            plan,
            out string errorMessage);

        Assert.False(valid);
        Assert.Contains("重复选择", errorMessage);
    }

    [Fact]
    public void Validator_WhenContributionsAreParaphrases_RejectsPlan()
    {
        GroupContext context = CreateContext("Alpha", "Beta");
        GroupConversationPlanningRequest request = CreateRequest(
            context,
            "聊聊周末去哪");
        GroupConversationTurnPlan plan = new()
        {
            AnchorMessageId = request.AnchorMessage!.Id,
            TopicFocus = "周末去处",
            TurnGoal = "给出两个不同角度",
            Speakers = new[]
            {
                CreateSpeakerPlan(
                    context.GroupChat.Members[0].Id,
                    request.AnchorMessage.Id,
                    "说明周末去旧书店会比较合适"),
                CreateSpeakerPlan(
                    context.GroupChat.Members[1].Id,
                    request.AnchorMessage.Id,
                    "说明周末去旧书店应该更合适")
            },
            SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection
        };

        bool valid = new GroupConversationPlanValidator().TryValidate(
            request,
            plan,
            out string errorMessage);

        Assert.False(valid);
        Assert.Contains("换词重复", errorMessage);
    }

    [Fact]
    public async Task RuleDirector_AutonomousOpening_RequiresInitiatorAndNeverTargetsLocalUser()
    {
        GroupContext context = CreateContext("Alpha", "Beta", "Gamma");
        AiAccount initiator = context.GroupChat.Members[1];
        GroupConversationPlanningRequest request = new()
        {
            GroupChat = context.GroupChat,
            Scenario = GroupConversationPlanningScenario.AutonomousOpening,
            Topic = "周末忽然降温，原定的户外拍摄要不要改成室内活动",
            RequiredSpeakerAiAccountId = initiator.Id,
            PreferredSpeakerAiAccountIds = new[]
            {
                initiator.Id,
                context.GroupChat.Members[0].Id
            }
        };
        RuleBasedGroupConversationDirector director = new(context.Planner);

        GroupConversationTurnPlan plan = await director.CreatePlanAsync(request);

        Assert.True(new GroupConversationPlanValidator().TryValidate(
            request,
            plan,
            out string errorMessage), errorMessage);
        Assert.Null(plan.AnchorMessageId);
        Assert.Equal(request.Topic, plan.TopicFocus);
        Assert.Equal(initiator.Id, plan.Speakers[0].SpeakerAiAccountId);
        Assert.All(plan.Speakers, speaker =>
            Assert.NotEqual(
                GroupConversationAudience.LocalUser,
                speaker.Audience));
    }

    [Fact]
    public void Validator_AutonomousPlanTargetingLocalUser_IsRejected()
    {
        GroupContext context = CreateContext("Alpha", "Beta", "Gamma");
        AiAccount initiator = context.GroupChat.Members[0];
        GroupConversationPlanningRequest request = new()
        {
            GroupChat = context.GroupChat,
            Scenario = GroupConversationPlanningScenario.AutonomousOpening,
            Topic = "最近看的电影",
            RequiredSpeakerAiAccountId = initiator.Id,
            PreferredSpeakerAiAccountIds = new[] { initiator.Id }
        };
        GroupConversationTurnPlan plan = new()
        {
            AnchorMessageId = null,
            TopicFocus = "最近看的电影",
            TurnGoal = "自然引入话题",
            Speakers = new[]
            {
                new GroupConversationSpeakerPlan
                {
                    SpeakerAiAccountId = initiator.Id,
                    ReplyTargetMessageId = null,
                    Audience = GroupConversationAudience.LocalUser,
                    Role = GroupConversationRole.ShiftTopic,
                    ResponseGoal = "自然开场",
                    NewContribution = "提起最近看的电影"
                }
            },
            SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection
        };

        bool valid = new GroupConversationPlanValidator().TryValidate(
            request,
            plan,
            out string errorMessage);

        Assert.False(valid);
        Assert.Contains("本地用户", errorMessage);
    }

    [Fact]
    public void Validator_AutonomousOpeningWithUnrelatedTopic_IsRejected()
    {
        GroupContext context = CreateContext("Alpha", "Beta", "Gamma");
        AiAccount initiator = context.GroupChat.Members[0];
        GroupConversationPlanningRequest request = new()
        {
            GroupChat = context.GroupChat,
            Scenario = GroupConversationPlanningScenario.AutonomousOpening,
            Topic = "降温后把户外拍摄改到室内",
            RequiredSpeakerAiAccountId = initiator.Id,
            PreferredSpeakerAiAccountIds = new[] { initiator.Id }
        };
        GroupConversationTurnPlan plan = new()
        {
            TopicFocus = "周末去咖啡馆",
            TurnGoal = "讨论周末去处",
            Speakers = new[]
            {
                new GroupConversationSpeakerPlan
                {
                    SpeakerAiAccountId = initiator.Id,
                    Audience = GroupConversationAudience.WholeGroup,
                    Role = GroupConversationRole.ShiftTopic,
                    ResponseGoal = "询问周末安排",
                    NewContribution = "推荐一家咖啡馆"
                }
            },
            SelectionStatus = AiSpeakerSelectionStatus.DefaultSelection
        };

        bool valid = new GroupConversationPlanValidator().TryValidate(
            request,
            plan,
            out string errorMessage);

        Assert.False(valid);
        Assert.Contains("偏离", errorMessage);
    }

    [Fact]
    public async Task ModelDirector_AutonomousOpening_AcceptsNullMessageTargetAndWholeGroupAudience()
    {
        GroupContext context = CreateContext("Alpha", "Beta", "Gamma");
        AiAccount initiator = context.GroupChat.Members[0];
        GroupConversationPlanningRequest request = new()
        {
            GroupChat = context.GroupChat,
            Scenario = GroupConversationPlanningScenario.AutonomousOpening,
            Topic = "最近做饭翻车的事",
            RequiredSpeakerAiAccountId = initiator.Id,
            PreferredSpeakerAiAccountIds = new[] { initiator.Id }
        };
        string json = JsonSerializer.Serialize(new
        {
            topicFocus = "最近做饭翻车的事",
            turnGoal = "由发起者自然说起最近的经历",
            coveredPoints = Array.Empty<string>(),
            unresolvedGoals = new[] { "自然引出其他成员的反应" },
            speakers = new[]
            {
                new
                {
                    speakerAiAccountId = initiator.Id,
                    replyTargetMessageId = (Guid?)null,
                    targetAiAccountId = (Guid?)null,
                    audience = "WholeGroup",
                    role = "ShiftTopic",
                    responseGoal = "自然说起刚发生的事",
                    newContribution = "提出把晚饭做简单一点的建议",
                    avoidedRepetition = Array.Empty<string>()
                }
            }
        });
        RecordingHandler handler = new(CreateResponse(json));
        OpenAiCompatibleGroupConversationDirector director = CreateDirector(
            context.Planner,
            handler);

        GroupConversationTurnPlan plan = await director.CreatePlanAsync(request);

        Assert.False(plan.UsedRuleFallback);
        GroupConversationSpeakerPlan speakerPlan = Assert.Single(plan.Speakers);
        Assert.Null(plan.AnchorMessageId);
        Assert.Null(speakerPlan.ReplyTargetMessageId);
        Assert.Equal(GroupConversationAudience.WholeGroup, speakerPlan.Audience);
        Assert.True(new GroupConversationPlanValidator().TryValidate(
            request,
            plan,
            out string errorMessage), errorMessage);
        Assert.Contains("AutonomousOpening", handler.RequestBody);
        using JsonDocument requestBody = JsonDocument.Parse(
            handler.RequestBody!);
        string systemPrompt = requestBody.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        string userPrompt = requestBody.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("不得替发起者虚构此前经历", userPrompt);
        Assert.Contains(
            "提出一种当下想去的场所类型和选择条件",
            systemPrompt);
    }

    private OpenAiCompatibleGroupConversationDirector CreateDirector(
        GroupChatReplyPlanner planner,
        HttpMessageHandler handler)
    {
        AiMessageGenerationOptions options = new()
        {
            BaseUrl = "https://api.example.test/",
            Model = "group-director-test",
            OutputValidationRetryCount = 0
        };
        HttpClient client = new(handler);
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiIdentityContinuityService identityContinuityService = new(
            new AiSelfMemoryService(factory),
            new AiInteractionDiagnosticLogService(factory));
        return new OpenAiCompatibleGroupConversationDirector(
            new OpenAiCompatibleChatClient(client, options),
            options,
            planner,
            new GroupConversationPlanValidator(),
            new GroupConversationContextService(
                factory,
                identityContinuityService,
                new AiWorldConversationContextService(
                    factory,
                    new AiWorldAwarenessService(factory),
                    new AiWorldKnowledgeService(factory),
                    new AiWorldKnowledgeCandidateExtractor())));
    }

    private GroupContext CreateContext(params string[] nicknames)
    {
        VocaChatDbContextFactory factory = _database.CreateDbContextFactory();
        AiAccountService accountService = new(factory);
        List<Guid> memberIds = new();
        foreach (string nickname in nicknames)
        {
            Assert.True(accountService.TryCreateAiAccount(
                nickname,
                $"{nickname} 的身份",
                "有自己的判断",
                "自然简洁",
                out AiAccount? account,
                out string accountError), accountError);
            memberIds.Add(Assert.IsType<AiAccount>(account).Id);
        }

        GroupChatService groupChatService = new(factory);
        Assert.True(groupChatService.TryCreateGroupChat(
            "导演测试群",
            memberIds,
            out GroupChat? groupChat,
            out string groupError), groupError);

        return new GroupContext(
            Assert.IsType<GroupChat>(groupChat),
            new GroupMessageService(factory),
            new GroupChatReplyPlanner(factory));
    }

    private static GroupConversationPlanningRequest CreateRequest(
        GroupContext context,
        string content)
    {
        Assert.True(context.MessageService.TrySaveUserMessage(
            context.GroupChat,
            content,
            out GroupMessage? message,
            out string errorMessage), errorMessage);
        GroupMessage anchor = Assert.IsType<GroupMessage>(message);
        return new GroupConversationPlanningRequest
        {
            GroupChat = context.GroupChat,
            AnchorMessage = anchor,
            RecentMessages = new[] { anchor }
        };
    }

    private static GroupConversationSpeakerPlan CreateSpeakerPlan(
        Guid speakerId,
        Guid targetMessageId,
        string newContribution) => new()
        {
            SpeakerAiAccountId = speakerId,
            ReplyTargetMessageId = targetMessageId,
            Audience = GroupConversationAudience.LocalUser,
            Role = GroupConversationRole.DirectAnswer,
            ResponseGoal = "回应用户",
            NewContribution = newContribution
        };

    private static string CreatePlanJson(
        Guid anchorMessageId,
        Guid firstSpeakerId,
        Guid secondSpeakerId) => JsonSerializer.Serialize(new
        {
            topicFocus = "当前安排是否合适",
            turnGoal = "先回答用户，再提出一个不同判断",
            coveredPoints = Array.Empty<string>(),
            unresolvedGoals = new[] { "给出两种有差异的判断" },
            speakers = new object[]
            {
                new
                {
                    speakerAiAccountId = firstSpeakerId,
                    replyTargetMessageId = anchorMessageId,
                    targetAiAccountId = (Guid?)null,
                    audience = "LocalUser",
                    role = "DirectAnswer",
                    responseGoal = "直接回答用户",
                    newContribution = "说明安排中合理的部分",
                    avoidedRepetition = Array.Empty<string>()
                },
                new
                {
                    speakerAiAccountId = secondSpeakerId,
                    replyTargetMessageId = anchorMessageId,
                    targetAiAccountId = firstSpeakerId,
                    audience = "SpecificAiAccount",
                    role = "Disagree",
                    responseGoal = "回应第一位成员的判断",
                    newContribution = "指出安排中可能被忽略的风险",
                    avoidedRepetition = new[] { "安排中合理的部分" }
                }
            }
        });

    private static HttpResponseMessage CreateResponse(string content)
    {
        string responseBody = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                responseBody,
                Encoding.UTF8,
                "application/json")
        };
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    private sealed record GroupContext(
        GroupChat GroupChat,
        GroupMessageService MessageService,
        GroupChatReplyPlanner Planner);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public string? RequestBody { get; private set; }

        public RecordingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }
}

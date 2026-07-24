using System.Net;
using System.Text;
using System.Text.Json;
using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证模型导演只能在业务硬约束内制定计划，并能安全退回规则导演。
/// </summary>
public sealed class ConversationDirectorTests
{
    [Fact]
    public async Task CreatePlanAsync_WithValidPlan_ReturnsModelDirection()
    {
        AiMessageGenerationRequest request = CreateTargetedRequest();
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Answer",
            request.ReplyTarget!.Message!.MessageId,
            "回家时间",
            "明确告诉对方预计几点回来",
            new[] { "更早的电影话题" })));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.False(plan.UsedRuleFallback);
        Assert.Equal(ConversationAction.Answer, plan.ActionPlan.Action);
        Assert.Equal(ConversationBeat.Clarify, plan.Beat);
        Assert.Equal("回家时间", plan.TopicFocus);
        Assert.Equal("明确告诉对方预计几点回来", plan.ResponseGoal);
        Assert.Equal(request.ReplyTarget.Message.MessageId, plan.TargetMessageId);
        Assert.Equal(new[] { "更早的电影话题" }, plan.AvoidedTopics);
        Assert.Equal(new[] { "已经问了回家时间" }, plan.CoveredPoints);
        Assert.Equal(new[] { "仍需给出具体时间" }, plan.UnresolvedGoals);
        Assert.Equal("补充一个明确时间", plan.NewContribution);
        Assert.Equal(
            new[] { "没有本人历史依据的昨晚行程" },
            plan.ForbiddenClaims);
        Assert.Equal(1, plan.SelectedMessageCount);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenContributionRepeatsCoveredPoint_UsesRuleFallback()
    {
        AiMessageGenerationRequest request = CreateTargetedRequest();
        string directorJson = JsonSerializer.Serialize(new
        {
            action = "Answer",
            questionMode = "Optional",
            beat = "Clarify",
            topicFocus = "回家时间",
            responseGoal = "明确告诉对方预计几点回来",
            messageCount = 1,
            targetMessageId = request.ReplyTarget!.Message!.MessageId,
            coveredPoints = new[] { "已经说明预计七点回来" },
            unresolvedGoals = new[] { "确认是否需要留晚饭" },
            newContribution = "已经说明预计七点回来",
            avoidedTopics = Array.Empty<string>(),
            forbiddenClaims = Array.Empty<string>()
        });
        OpenAiCompatibleConversationDirector director = CreateDirector(
            new RecordingHandler(CreateResponse(directorJson)));

        ConversationDirectionPlan plan = await director
            .CreatePlanAsync(request);

        Assert.True(plan.UsedRuleFallback);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenTargetIsChanged_UsesRuleFallback()
    {
        AiMessageGenerationRequest request = CreateTargetedRequest();
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Answer",
            Guid.NewGuid(),
            "别的话题",
            "回应别的消息")));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.True(plan.UsedRuleFallback);
        Assert.Equal(request.ReplyTarget!.Message!.MessageId, plan.TargetMessageId);
        Assert.Equal(ConversationAction.Answer, plan.ActionPlan.Action);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenQuestionIsNotAnswered_UsesRuleFallback()
    {
        AiMessageGenerationRequest request = CreateTargetedRequest();
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "ShiftTopic",
            request.ReplyTarget!.Message!.MessageId,
            "旁边的话题",
            "避开问题")));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.True(plan.UsedRuleFallback);
        Assert.Equal(ConversationAction.Answer, plan.ActionPlan.Action);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenProviderFails_UsesRuleFallback()
    {
        AiMessageGenerationRequest request = CreateTargetedRequest();
        RecordingHandler handler = new(new HttpResponseMessage(
            HttpStatusCode.ServiceUnavailable));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.True(plan.UsedRuleFallback);
        Assert.Equal(ConversationAction.Answer, plan.ActionPlan.Action);
    }

    [Fact]
    public async Task CreatePlanAsync_SeparatesTargetFromOlderBackground()
    {
        AiMessageGenerationRequest request = CreateTargetedRequest();
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Answer",
            request.ReplyTarget!.Message!.MessageId,
            "回家时间",
            "给出直接答案")));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        await director.CreatePlanAsync(request);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("本轮必须回应的目标消息", userPrompt);
        Assert.Contains("你什么时候回来？", userPrompt);
        Assert.Contains("更早背景（不能替代上面的目标消息）", userPrompt);
        Assert.Contains("昨天看的电影不错", userPrompt);
    }

    [Fact]
    public async Task CreatePlanAsync_WithConversationAnchor_PreservesOriginalRequest()
    {
        AiMessageGenerationRequest baseRequest = CreateTargetedRequest();
        AiDialogueMessage anchor = new(
            "我",
            "前一个人先选择，后一个人回应前面的选择并说明自己的决定。",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = baseRequest with
        {
            Scenario = AiMessageGenerationScenario.GroupFollowUpReply,
            ConversationAnchor = anchor
        };
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Answer",
            request.ReplyTarget!.Message!.MessageId,
            "后一个人的决定",
            "回应前一人的选择并给出自己的决定")));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        await director.CreatePlanAsync(request);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("整轮互动的原始起点", userPrompt);
        Assert.Contains(anchor.Content, userPrompt);
        Assert.Contains("本轮必须回应的目标消息", userPrompt);
    }

    [Fact]
    public async Task CreatePlanAsync_UsesOnlyCurrentSpeakerDirectionalMemories()
    {
        AiMessageGenerationRequest baseRequest = CreateTargetedRequest();
        AiAccount other = new(
            "7654321",
            "小岚",
            string.Empty,
            string.Empty,
            string.Empty);
        AiMessageGenerationRequest request = baseRequest with
        {
            OtherParticipants = new[] { other },
            RelationshipTarget = other,
            RelevantMemories = new[]
            {
                new AiConversationMemory(
                    baseRequest.Speaker.Id,
                    other.Id,
                    other.Nickname,
                    AiMemoryType.Preference,
                    "小岚更喜欢安静的展览",
                    new DateTime(2026, 7, 10)),
                new AiConversationMemory(
                    other.Id,
                    baseRequest.Speaker.Id,
                    baseRequest.Speaker.Nickname,
                    AiMemoryType.Habit,
                    "反向记忆不能被当前发言者看到",
                    new DateTime(2026, 7, 11))
            }
        };
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Answer",
            request.ReplyTarget!.Message!.MessageId,
            "回家时间",
            "给出直接答案")));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        await director.CreatePlanAsync(request);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string systemPrompt = body.RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("长期记忆只代表当前发言者", systemPrompt);
        Assert.Contains("小岚更喜欢安静的展览", userPrompt);
        Assert.DoesNotContain("反向记忆不能被当前发言者看到", userPrompt);
    }

    [Fact]
    public async Task CreatePlanAsync_ParsesCurrentSpeakerSelfMemoryPlan()
    {
        AiMessageGenerationRequest baseRequest = CreateTargetedRequest();
        Guid memoryId = Guid.NewGuid();
        AiMessageGenerationRequest request = baseRequest with
        {
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    memoryId,
                    baseRequest.Speaker.Id,
                    AiSelfMemoryType.OngoingActivity,
                    "最近正在准备秋季插画展",
                    "current.project",
                    AiSelfMemoryFactNature.Objective,
                    AiSelfMemoryMutability.Mutable,
                    AiSelfMemoryTrustLevel.UserCanon,
                    CharacterWorld.DefaultWorldId,
                    AiSelfMemorySource.User,
                    90,
                    true,
                    new DateTime(2026, 7, 18),
                    new DateTime(2026, 7, 20))
            }
        };
        string directorJson = JsonSerializer.Serialize(new
        {
            action = "Answer",
            questionMode = "Optional",
            beat = "Clarify",
            topicFocus = "插画展进度",
            responseGoal = "说明当前准备进度",
            messageCount = 1,
            targetMessageId = request.ReplyTarget!.Message!.MessageId,
            coveredPoints = Array.Empty<string>(),
            unresolvedGoals = new[] { "说明目前进度" },
            newContribution = "补充正在整理最后一批作品",
            avoidedTopics = Array.Empty<string>(),
            forbiddenClaims = Array.Empty<string>(),
            referencedSelfMemoryIds = new[] { memoryId },
            selfMemoryProposals = new[]
            {
                new
                {
                    operation = "Add",
                    targetMemoryId = (string?)null,
                    subjectAiAccountId = baseRequest.Speaker.Id,
                    characterWorldId =
                        baseRequest.Speaker.CharacterWorldId,
                    type = "OngoingActivity",
                    factKey = "current.project",
                    factNature = "Objective",
                    mutability = "Evolving",
                    summary = "正在为插画展整理最后一批作品",
                    reason = "本轮准备明确表达当前进度"
                }
            }
        });
        RecordingHandler handler = new(CreateResponse(directorJson));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.Equal(new[] { memoryId }, plan.ReferencedSelfMemoryIds);
        AiSelfMemoryProposal proposal = Assert.Single(plan.SelfMemoryProposals);
        Assert.Equal(AiSelfMemoryProposalOperation.Add, proposal.Operation);
        Assert.Equal(AiSelfMemoryType.OngoingActivity, proposal.Type);
        Assert.Equal(baseRequest.Speaker.Id, proposal.SubjectAiAccountId);
        Assert.Equal(
            baseRequest.Speaker.CharacterWorldId,
            proposal.CharacterWorldId);
        Assert.Equal("current.project", proposal.FactKey);
        Assert.Equal(AiSelfMemoryFactNature.Objective, proposal.FactNature);
        Assert.Equal(AiSelfMemoryMutability.Evolving, proposal.Mutability);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains(memoryId.ToString(), userPrompt);
        Assert.Contains("最近正在准备秋季插画展", userPrompt);
    }

    [Fact]
    public async Task CreatePlanAsync_UserPrivateChat_SelectsCountWithinRangeAndWritesSceneBoundary()
    {
        AiMessageGenerationRequest request = CreateTargetedRequest() with
        {
            AllowedMessageCountRange = new AiMessageCountRange(1, 3)
        };
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Answer",
            request.ReplyTarget!.Message!.MessageId,
            "回家时间",
            "明确告诉对方预计几点回来",
            messageCount: 2)));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.Equal(2, plan.SelectedMessageCount);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("一对一私信", userPrompt);
        Assert.Contains("当前频道不是群聊", userPrompt);
        Assert.Contains("1 到 3 条", userPrompt);
        Assert.Contains("所属角色世界：现实世界", userPrompt);
        Assert.Contains("新名称本身不构成违规", userPrompt);
    }

    [Fact]
    public async Task CreatePlanAsync_ResolvesThirdPartyReferenceAndWritesTimeGap()
    {
        AiMessageGenerationRequest baseRequest = CreateTargetedRequest();
        DateTime targetSentAt = new(2026, 7, 22, 20, 0, 0);
        AiDialogueMessage oldMessage = new(
            "小语",
            "咖啡馆老板熟了以后会露出冷幽默",
            MessageSenderType.AiAccount,
            baseRequest.Speaker.Id,
            Guid.NewGuid(),
            targetSentAt.AddDays(-2));
        AiDialogueMessage target = new(
            "我",
            "那个人对谁都这样吗？",
            MessageSenderType.User,
            null,
            Guid.NewGuid(),
            targetSentAt);
        AiMessageGenerationRequest request = baseRequest with
        {
            FocusContent = target.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(target),
            RecentMessages = new[] { oldMessage, target }
        };
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Answer",
            target.MessageId,
            "咖啡馆老板的冷幽默",
            "回应老板是否对谁都这样",
            referenceStatus: "Resolved",
            referenceResolution: "那个人指此前谈到的咖啡馆老板",
            factOwnership: new[] { "冷幽默是咖啡馆老板的特点，不属于当前发言者" })));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.Equal(
            ConversationReferenceStatus.Resolved,
            plan.ReferencePlan.Status);
        Assert.Contains("咖啡馆老板", plan.ReferencePlan.ResolutionSummary);
        Assert.Single(plan.ReferencePlan.FactOwnershipConstraints);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("约 2 天，属于跨时间续聊", userPrompt);
        Assert.Contains("必须结合带事实归属的历史消息解析指向", userPrompt);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenReferenceIsAmbiguous_CanAskForClarification()
    {
        AiMessageGenerationRequest request = CreateTargetedRequest();
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Ask",
            request.ReplyTarget!.Message!.MessageId,
            "无法确认的人物",
            "自然确认对方指的是谁",
            referenceStatus: "Ambiguous",
            referenceResolution: "最近记录中有两个人都可能被称为那个人")));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.False(plan.UsedRuleFallback);
        Assert.Equal(ConversationAction.Ask, plan.ActionPlan.Action);
        Assert.Equal(
            ConversationReferenceStatus.Ambiguous,
            plan.ReferencePlan.Status);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenAutonomousGroupDirectionDrifts_UsesRuleFallback()
    {
        AiAccount speaker = new(
            "1234567",
            "小语",
            string.Empty,
            string.Empty,
            string.Empty);
        AiMessageGenerationRequest request = new()
        {
            Scenario = AiMessageGenerationScenario.AutonomousGroupChat,
            Speaker = speaker,
            Topic = "降温后把户外拍摄改到室内",
            FocusContent = "降温后把户外拍摄改到室内",
            ReplyTarget = AiDialogueReplyTarget.OpenTopic(),
            ExpectedMessageCount = 1,
            GroupConversationPlan = new GroupConversationSpeakerPlan
            {
                SpeakerAiAccountId = speaker.Id,
                Audience = GroupConversationAudience.WholeGroup,
                Role = GroupConversationRole.ShiftTopic,
                ResponseGoal = "自然讨论拍摄地点",
                NewContribution = "提出改到室内拍摄"
            }
        };
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "ShiftTopic",
            Guid.Empty,
            "周末去咖啡馆",
            "推荐一家咖啡馆")));
        OpenAiCompatibleConversationDirector director = CreateDirector(handler);

        ConversationDirectionPlan plan = await director.CreatePlanAsync(request);

        Assert.True(plan.UsedRuleFallback);
        Assert.Contains("户外拍摄", plan.TopicFocus);
        Assert.Equal("提出改到室内拍摄", plan.NewContribution);
    }

    [Fact]
    public async Task CreatePlanAsync_WithWorldContext_UsesSpeakerKnowledgeBoundary()
    {
        AiMessageGenerationRequest baseRequest = CreateTargetedRequest();
        CharacterWorld targetWorld = new(
            "基沃托斯",
            "由多个学院自治区组成的学园都市。");
        AiAccount target = new(
            "7654321",
            "小白",
            "阿拜多斯高中的学生",
            "冷静",
            "简短");
        target.AssignCharacterWorld(targetWorld);
        AiConversationWorldKnowledge knowledge = new(
            Guid.NewGuid(),
            baseRequest.Speaker.Id,
            targetWorld.Id,
            target.Id,
            "小白提到：阿拜多斯是一所受到沙漠化影响的高中。",
            AiWorldKnowledgeTrustLevel.DirectStatement,
            80,
            DateTime.Now);
        AiMessageGenerationRequest request = baseRequest with
        {
            Scenario = AiMessageGenerationScenario.AutonomousPrivateChat,
            OtherParticipants = new[] { target },
            RelationshipTarget = target,
            WorldConversationContext = new AiWorldConversationContext(
                AiParallelWorldAwarenessState.Unaware,
                AiWorldAwarenessState.AnomalyObserved,
                target.Id,
                targetWorld.Id,
                VisibleSubjectWorldName: null,
                IsNewlyInformedByCurrentMessage: false,
                AiWorldInquiryMode.ExploreBackgroundDifference,
                new[] { knowledge })
        };
        RecordingHandler handler = new(CreateResponse(CreateDirectorJson(
            "Answer",
            request.ReplyTarget!.Message!.MessageId,
            "对方提到的学校",
            "回应已经听到的沙漠化情况")));

        await CreateDirector(handler).CreatePlanAsync(request);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string userPrompt = body.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("AnomalyObserved", userPrompt);
        Assert.Contains("阿拜多斯是一所受到沙漠化影响的高中", userPrompt);
        Assert.Contains("尚不能断言双方来自不同世界", userPrompt);
        Assert.DoesNotContain("跨世界远程通信对象：小白", userPrompt);
        Assert.DoesNotContain("当前对象所在世界可称为“基沃托斯”", userPrompt);
    }

    private static OpenAiCompatibleConversationDirector CreateDirector(
        HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://api.example.test/")
        };
        AiMessageGenerationOptions options = new()
        {
            BaseUrl = "https://api.example.test/v1/",
            Model = "director-test-model",
            OutputValidationRetryCount = 0
        };
        ConversationActionPlanner planner = new(new ConstantRandom(0.2));
        return new OpenAiCompatibleConversationDirector(
            new OpenAiCompatibleChatClient(httpClient, options),
            options,
            new AiConversationContextBuilder(),
            planner);
    }

    private static AiMessageGenerationRequest CreateTargetedRequest()
    {
        AiAccount speaker = new(
            "1234567",
            "小语",
            "常常晚归的朋友",
            "温和",
            "简短直接");
        AiDialogueMessage oldMessage = new(
            "小语",
            "昨天看的电影不错",
            MessageSenderType.AiAccount,
            speaker.Id,
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(-5));
        AiDialogueMessage target = new(
            "我",
            "你什么时候回来？",
            MessageSenderType.User,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow);

        return new AiMessageGenerationRequest
        {
            Scenario = AiMessageGenerationScenario.UserPrivateChat,
            Speaker = speaker,
            FocusContent = target.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(target),
            RecentMessages = new[] { oldMessage, target },
            ExpectedMessageCount = 1
        };
    }

    private static string CreateDirectorJson(
        string action,
        Guid targetMessageId,
        string topicFocus,
        string responseGoal,
        IReadOnlyList<string>? avoidedTopics = null,
        int messageCount = 1,
        string referenceStatus = "None",
        string referenceResolution = "",
        IReadOnlyList<string>? factOwnership = null) =>
        JsonSerializer.Serialize(new
        {
            action,
            questionMode = action == "Ask" ? "Natural" : "Optional",
            beat = "Clarify",
            topicFocus,
            responseGoal,
            messageCount,
            targetMessageId = targetMessageId == Guid.Empty
                ? string.Empty
                : targetMessageId.ToString(),
            coveredPoints = new[] { "已经问了回家时间" },
            unresolvedGoals = new[] { "仍需给出具体时间" },
            newContribution = "补充一个明确时间",
            avoidedTopics = avoidedTopics ?? Array.Empty<string>(),
            forbiddenClaims = new[] { "没有本人历史依据的昨晚行程" },
            referenceStatus,
            referenceResolution,
            factOwnership = factOwnership ?? Array.Empty<string>()
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

    private sealed class ConstantRandom : Random
    {
        private readonly double _value;

        public ConstantRandom(double value)
        {
            _value = value;
        }

        protected override double Sample() => _value;
    }
}

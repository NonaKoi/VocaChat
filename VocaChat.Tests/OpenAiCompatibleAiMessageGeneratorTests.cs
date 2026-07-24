using System.Net;
using System.Text;
using System.Text.Json;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

/// <summary>
/// 验证 OpenAI 兼容生成器的 HTTP 契约和模型输出保护，不访问真实 Ollama。
/// </summary>
public sealed class OpenAiCompatibleAiMessageGeneratorTests
{
    [Fact]
    public async Task GenerateMessagesAsync_WithValidJson_ReturnsExpectedMessages()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"第一条\",\"第二条\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);

        IReadOnlyList<string> messages = await generator.GenerateMessagesAsync(
            CreateRequest(expectedMessageCount: 2));

        Assert.Equal(new[] { "第一条", "第二条" }, messages);
        Assert.NotNull(handler.RequestBody);
        using JsonDocument request = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal(
            "vocachat-test-model",
            request.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            "json_object",
            request.RootElement
                .GetProperty("response_format")
                .GetProperty("type")
                .GetString());
        Assert.Equal(
            "disabled",
            request.RootElement
                .GetProperty("thinking")
                .GetProperty("type")
                .GetString());
        Assert.False(request.RootElement.TryGetProperty(
            "reasoning_effort",
            out _));
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithFourMessages_PreservesAllBubbles()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"one\",\"two\",\"three\",\"four\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);

        IReadOnlyList<string> messages = await generator.GenerateMessagesAsync(
            CreateRequest(expectedMessageCount: 4));

        Assert.Equal(new[] { "one", "two", "three", "four" }, messages);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithWrongMessageCount_RejectsOutput()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"只有一条\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);

        AiMessageGenerationException exception = await Assert.ThrowsAsync<
            AiMessageGenerationException>(() =>
            generator.GenerateMessagesAsync(CreateRequest(expectedMessageCount: 2)));

        Assert.Contains("应生成 2 条", exception.Message);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithBlankMessage_RejectsOutput()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"   \"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);

        AiMessageGenerationException exception = await Assert.ThrowsAsync<
            AiMessageGenerationException>(() =>
            generator.GenerateMessagesAsync(CreateRequest(expectedMessageCount: 1)));

        Assert.Contains("空白消息", exception.Message);
    }

    [Fact]
    public async Task GenerateMessagesAsync_DirectAnswerMayReuseUserTopicWords()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"我觉得周末去旧书店挺合适的\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1);
        request = request with
        {
            RecentMessages = new[]
            {
                new AiDialogueMessage(
                    "我",
                    "你觉得周末去旧书店合适吗？",
                    MessageSenderType.User,
                    null)
            }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "我觉得周末去旧书店挺合适的" },
            messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_NearDuplicateBatch_Retries()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"周末去旧书店会比较合适\","
                + "\"我觉得周末去旧书店会比较合适\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"旧书店可以慢慢逛\","
                + "\"如果下雨就改去附近咖啡馆\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(CreateRequest(2));

        Assert.Equal(
            new[] { "旧书店可以慢慢逛", "如果下雨就改去附近咖啡馆" },
            messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenEndpointFails_ReturnsSafeError()
    {
        RecordingHandler handler = new(new HttpResponseMessage(
            HttpStatusCode.ServiceUnavailable));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);

        AiMessageGenerationException exception = await Assert.ThrowsAsync<
            AiMessageGenerationException>(() =>
            generator.GenerateMessagesAsync(CreateRequest(expectedMessageCount: 1)));

        Assert.Contains("错误状态 503", exception.Message);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenFirstOutputCountIsWrong_RetriesOnce()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"只有一条\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"第一条\",\"第二条\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);

        IReadOnlyList<string> messages = await generator.GenerateMessagesAsync(
            CreateRequest(expectedMessageCount: 2));

        Assert.Equal(new[] { "第一条", "第二条" }, messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WritesActionAndHumanRelationshipGuidance()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"嗯，先歇会儿吧\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            SpeakerToOtherRelationshipScore = 85,
            OtherToSpeakerRelationshipScore = 60,
            ActionPlan = new ConversationActionPlan(
                ConversationAction.Comfort,
                ConversationMessageLength.VeryShort,
                ConversationDirectness.Partial,
                ConversationQuestionMode.None,
                ConversationEmotionVisibility.Open,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Close,
                ConversationRelationshipBalance.SpeakerMoreInvested,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: true)
        };

        await generator.GenerateMessagesAsync(request);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string systemPrompt = body.RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("只用于内化身份", systemPrompt);
        Assert.Contains("调度性要求只用于内部执行", systemPrompt);
        Assert.Contains("新人物、新地点和新名词本身不需要预先出现", systemPrompt);
        Assert.Contains("所属角色世界：现实世界", systemPrompt);
        Assert.Contains("本次交流动作：以陪伴和理解为主", userPrompt);
        Assert.Contains("关系亲近", userPrompt);
        Assert.Contains("你更在意对方", userPrompt);
        Assert.DoesNotContain("关系分", userPrompt);
        Assert.DoesNotContain("85", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithDirectorPlan_WritesSemanticGoal()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"今晚会继续画。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1);
        ConversationDirectionPlan direction = new(
            request.ActionPlan!,
            ConversationBeat.Resolve,
            "今晚是否继续画画",
            "直接说明今晚会不会继续画",
            Guid.Empty,
            new[] { "已经确认会继续画" },
            new[] { "还没有说明结束时间" },
            "补充今晚预计结束的时间",
            new[] { "昨天的电影" },
            new[] { "没有本人历史依据的昨日行程" },
            usedRuleFallback: false);

        await generator.GenerateMessagesAsync(request with
        {
            DirectionPlan = direction
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("导演指定的话题焦点：今晚是否继续画画", userPrompt);
        Assert.Contains("导演指定的回应目标：直接说明今晚会不会继续画", userPrompt);
        Assert.Contains("当前会话节拍：落定", userPrompt);
        Assert.Contains("最近已经表达过的内容（不要换句话重复）：已经确认会继续画", userPrompt);
        Assert.Contains("原始要求中仍需处理的部分：还没有说明结束时间", userPrompt);
        Assert.Contains("本轮必须新增的内容：补充今晚预计结束的时间", userPrompt);
        Assert.Contains("本轮不要恢复的话题：昨天的电影", userPrompt);
        Assert.Contains("本轮不得声称的事实：没有本人历史依据的昨日行程", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithoutActionPlan_RejectsRequestBeforeHttpCall()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"不会被使用\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            ActionPlan = null
        };

        AiMessageGenerationException exception = await Assert.ThrowsAsync<
            AiMessageGenerationException>(() =>
            generator.GenerateMessagesAsync(request));

        Assert.Contains("缺少行为与表达计划", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_StyleLengthDoesNotRejectCompleteReply()
    {
        const string completeReply =
            "这是一条超过旧有十八字符限制、但仍然自然并且把当前意思表达完整的回复。";
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            $"{{\"messages\":[\"{completeReply}\"]}}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            ActionPlan = new ConversationActionPlan(
                ConversationAction.Comfort,
                ConversationMessageLength.VeryShort,
                ConversationDirectness.Partial,
                ConversationQuestionMode.None,
                ConversationEmotionVisibility.Natural,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Unknown,
                ConversationRelationshipBalance.Unknown,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: true)
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { completeReply }, messages);
        Assert.Equal(1, handler.CallCount);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string systemPrompt = body.RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("当前频道是一对一私信", systemPrompt);
        Assert.Contains("仍须完成当前动作的核心内容", userPrompt);
        Assert.DoesNotContain("不超过 18 个字符", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenOutputUsesFixedServiceQuestion_Retries()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"你愿意和我说说吗？\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"听着就挺累的\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            ActionPlan = new ConversationActionPlan(
                ConversationAction.Comfort,
                ConversationMessageLength.VeryShort,
                ConversationDirectness.Partial,
                ConversationQuestionMode.None,
                ConversationEmotionVisibility.Natural,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Unknown,
                ConversationRelationshipBalance.Unknown,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: true)
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "听着就挺累的" }, messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument retryRequest = JsonDocument.Parse(
            Assert.IsType<string>(handler.RequestBodies[1]));
        string retryPrompt = retryRequest.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains(
            "本轮禁止疑问句",
            retryPrompt);
        Assert.Contains(
            "改成“我们可以……”",
            retryPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenDirectorAllowsNaturalQuestion_AcceptsIt()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"那你今晚还打算继续画吗？\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            ActionPlan = new ConversationActionPlan(
                ConversationAction.Ask,
                ConversationMessageLength.Short,
                ConversationDirectness.Direct,
                ConversationQuestionMode.Natural,
                ConversationEmotionVisibility.Natural,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Unknown,
                ConversationRelationshipBalance.Unknown,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: false)
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "那你今晚还打算继续画吗？" }, messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_UnrecoveredSoftQuestionIssue_KeepsCandidateReply()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"那你今晚还打算继续画吗？\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            ActionPlan = new ConversationActionPlan(
                ConversationAction.React,
                ConversationMessageLength.Short,
                ConversationDirectness.Partial,
                ConversationQuestionMode.None,
                ConversationEmotionVisibility.Natural,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Unknown,
                ConversationRelationshipBalance.Unknown,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: true)
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "那你今晚还打算继续画吗？" }, messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_LabelsFactsBySenderIdentity()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"听起来挺热闹的\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiAccount other = new(
            "7654321",
            "小岚",
            string.Empty,
            string.Empty,
            string.Empty);
        AiMessageGenerationRequest request = baseRequest with
        {
            OtherParticipants = new[] { other },
            RecentMessages = new[]
            {
                new AiDialogueMessage(
                    baseRequest.Speaker.Nickname,
                    "我最近一直在加班",
                    MessageSenderType.AiAccount,
                    baseRequest.Speaker.Id),
                new AiDialogueMessage(
                    other.Nickname,
                    "我昨晚去杭州看演出",
                    MessageSenderType.AiAccount,
                    other.Id),
                new AiDialogueMessage(
                    "我",
                    "我今天很累",
                    MessageSenderType.User,
                    null)
            },
            ActionPlan = new ConversationActionPlan(
                ConversationAction.React,
                ConversationMessageLength.VeryShort,
                ConversationDirectness.Partial,
                ConversationQuestionMode.None,
                ConversationEmotionVisibility.Natural,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Unknown,
                ConversationRelationshipBalance.Unknown,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: true)
        };

        await generator.GenerateMessagesAsync(request);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string systemPrompt = body.RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("绝不能改写成你亲身", systemPrompt);
        Assert.Contains(
            "本人过去说过",
            userPrompt);
        Assert.Contains(
            "其他好友“小岚”说过",
            userPrompt);
        Assert.Contains(
            "本地用户说过",
            userPrompt);
        Assert.Contains("只能转述、回应或形成听闻", userPrompt);
        Assert.Contains("只能视为用户提供的上下文", userPrompt);
        Assert.Contains("我昨晚去杭州看演出", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WritesOnlyValidatedDirectionalMemories()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"那就别忘了带伞\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
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
                    AiMemoryType.Habit,
                    "小岚下雨天经常忘记带伞",
                    new DateTime(2026, 7, 10)),
                new AiConversationMemory(
                    other.Id,
                    baseRequest.Speaker.Id,
                    baseRequest.Speaker.Nickname,
                    AiMemoryType.Preference,
                    "这是对方持有的反向记忆",
                    new DateTime(2026, 7, 11))
            }
        };

        await generator.GenerateMessagesAsync(request);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string systemPrompt = body.RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("本人对对方的长期记忆", systemPrompt);
        Assert.Contains("小岚下雨天经常忘记带伞", userPrompt);
        Assert.Contains("[习惯] 关于小岚", userPrompt);
        Assert.DoesNotContain("这是对方持有的反向记忆", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_SharedMemoryCanGroundPastSharedExperience()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"上次我们一起去了蓝桉剧场\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
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
                    AiMemoryType.SharedExperience,
                    "两个人上次一起去了蓝桉剧场",
                    new DateTime(2026, 7, 10))
            }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "上次我们一起去了蓝桉剧场" }, messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_CurrentSelfMemoryGroundsDynamicFirstPersonFact()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"我最近正在准备秋季插画展\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiMessageGenerationRequest request = baseRequest with
        {
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    Guid.NewGuid(),
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

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "我最近正在准备秋季插画展" }, messages);
        Assert.Equal(1, handler.CallCount);
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
        Assert.Contains("来源为 Director 的个人记忆可以维持本人叙事", systemPrompt);
        Assert.Contains("[用户确认]", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenNewVenueNameHasNoPriorSource_AllowsNarrativeName()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"推荐去「水巷茶室」，雨天很安静\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"可以找一家安静、适合久坐的茶室\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(CreateRequest(1));

        Assert.Equal(new[] { "推荐去「水巷茶室」，雨天很安静" }, messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_RealityStatusWithoutSource_RetriesAndRecordsRecovery()
    {
        using SqliteTestDatabase database = new();
        AiInteractionDiagnosticLogService diagnosticLogService = new(
            database.CreateDbContextFactory());
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"水巷茶室今晚有场演出\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"水巷茶室听起来不错，具体安排先确认一下\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1,
            diagnosticLogService: diagnosticLogService);
        Guid privateChatId = Guid.NewGuid();
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            UsageCorrelation = new AiModelUsageCorrelation
            {
                PrivateChatId = privateChatId
            }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "水巷茶室听起来不错，具体安排先确认一下" },
            messages);
        Assert.Equal(2, handler.CallCount);
        AiInteractionDiagnosticLog log = Assert.Single(
            diagnosticLogService.GetRecent());
        Assert.Equal(AiInteractionDiagnosticCode.MessageGenerationFailed, log.Code);
        Assert.Equal(privateChatId, log.ConversationId);
        Assert.True(log.WasRecovered);
        Assert.Contains("现实世界营业、活动或经营信息", log.Detail);
    }

    [Fact]
    public async Task GenerateMessagesAsync_UnrecoveredHardViolation_ReturnsSafeReplyAndLogsRecovery()
    {
        using SqliteTestDatabase database = new();
        AiInteractionDiagnosticLogService diagnosticLogService = new(
            database.CreateDbContextFactory());
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"水巷茶室今晚有场演出\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            diagnosticLogService: diagnosticLogService);

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(CreateRequest(1));

        Assert.Equal(new[] { "这件事我先不乱下结论。" }, messages);
        AiInteractionDiagnosticLog log = Assert.Single(
            diagnosticLogService.GetRecent());
        Assert.True(log.WasRecovered);
        Assert.Contains("现实世界营业、活动或经营信息", log.Detail);
    }

    [Fact]
    public async Task GenerateMessagesAsync_CustomWorld_AllowsNewVenueAndCurrentWorldEvent()
    {
        CharacterWorld world = new(
            "镜海群岛",
            "浮空岛之间以潮汐列车往来，夜间会举办潮汐歌会。");
        AiAccount speaker = new(
            "7654321",
            "澜音",
            "镜海群岛的列车记录员",
            "安静",
            "简短自然");
        speaker.AssignCharacterWorld(world);
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"星渊茶室今晚有场潮汐歌会\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Speaker = speaker
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "星渊茶室今晚有场潮汐歌会" }, messages);
        Assert.Equal(1, handler.CallCount);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string systemPrompt = body.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("所属角色世界：镜海群岛", systemPrompt);
        Assert.Contains("浮空岛之间以潮汐列车往来", systemPrompt);
        Assert.Contains("允许自然提到符合该世界说明的新人物、地点和名词", systemPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_CrossWorldPhysicalVisit_RetriesAsRemoteCommunication()
    {
        CharacterWorld speakerWorld = new(
            "基沃托斯",
            "由多个学园自治区域组成的超大型学园都市。");
        CharacterWorld otherWorld = new(
            "雾海列岛",
            "群岛漂浮在常年雾海之上，以雾航船往来。");
        AiAccount speaker = new(
            "7654321",
            "砂狼白子",
            "阿拜多斯高中的学生",
            "冷静",
            "简短直接");
        speaker.AssignCharacterWorld(speakerWorld);
        AiAccount other = new(
            "7654322",
            "岚汐",
            "雾海列岛的航路记录员",
            "温和",
            "自然");
        other.AssignCharacterWorld(otherWorld);
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"我去雾海列岛找你，咱们见面聊\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"雾海列岛听起来很特别，隔着通讯聊也不错\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Speaker = speaker,
            OtherParticipants = new[] { other },
            RelationshipTarget = other
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "雾海列岛听起来很特别，隔着通讯聊也不错" },
            messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string systemPrompt = body.RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        string retryPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.DoesNotContain("当前交流包含不同角色世界", systemPrompt);
        Assert.Contains("没有经过业务层验证的跨世界认知上下文", systemPrompt);
        Assert.Contains("不能无依据声称已经线下见面", systemPrompt);
        Assert.Contains("只能按远程通信理解", retryPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_CrossWorldHypothesis_RemainsAllowed()
    {
        CharacterWorld speakerWorld = new(
            "基沃托斯",
            "由多个学园自治区域组成的超大型学园都市。");
        CharacterWorld otherWorld = new(
            "雾海列岛",
            "群岛漂浮在常年雾海之上。");
        AiAccount speaker = new(
            "7654321",
            "砂狼白子",
            string.Empty,
            string.Empty,
            string.Empty);
        speaker.AssignCharacterWorld(speakerWorld);
        AiAccount other = new(
            "7654322",
            "岚汐",
            string.Empty,
            string.Empty,
            string.Empty);
        other.AssignCharacterWorld(otherWorld);
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"如果真能去雾海列岛看看就好了\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Speaker = speaker,
            OtherParticipants = new[] { other },
            RelationshipTarget = other
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "如果真能去雾海列岛看看就好了" }, messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_ForeignWorldFirstPersonExperience_RetriesAsHearsay()
    {
        CharacterWorld speakerWorld = new(
            "基沃托斯",
            "由多个学园自治区域组成的超大型学园都市。");
        CharacterWorld otherWorld = new(
            "雾海列岛",
            "群岛漂浮在常年雾海之上。");
        AiAccount speaker = new(
            "7654321",
            "砂狼白子",
            string.Empty,
            string.Empty,
            string.Empty);
        speaker.AssignCharacterWorld(speakerWorld);
        AiAccount other = new(
            "7654322",
            "岚汐",
            string.Empty,
            string.Empty,
            string.Empty);
        other.AssignCharacterWorld(otherWorld);
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"我在雾海列岛记录过潮汐\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"你说的雾海列岛潮汐听起来很特别\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Speaker = speaker,
            OtherParticipants = new[] { other },
            RelationshipTarget = other
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "你说的雾海列岛潮汐听起来很特别" },
            messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenUserProvidesVenueName_AllowsReference()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"可以去「水巷茶室」看看，出发前先确认营业时间\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            FocusContent = "我想去「水巷茶室」看看",
            RecentMessages = new[]
            {
                new AiDialogueMessage(
                    "我",
                    "我想去「水巷茶室」看看",
                    MessageSenderType.User,
                    null)
            }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "可以去「水巷茶室」看看，出发前先确认营业时间" },
            messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_DirectorMemorySupportsPersonalNarrative()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"我上周在城北旧火车站拍过一组雨后照片\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiMessageGenerationRequest request = baseRequest with
        {
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    Guid.NewGuid(),
                    baseRequest.Speaker.Id,
                    AiSelfMemoryType.Experience,
                    "上周在城北旧火车站拍过一组雨后照片",
                    "experience.old-station",
                    AiSelfMemoryFactNature.Narrative,
                    AiSelfMemoryMutability.Immutable,
                    AiSelfMemoryTrustLevel.NarrativeCandidate,
                    CharacterWorld.DefaultWorldId,
                    AiSelfMemorySource.Director,
                    80,
                    false,
                    new DateTime(2026, 7, 18),
                    new DateTime(2026, 7, 20))
            }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "我上周在城北旧火车站拍过一组雨后照片" },
            messages);
        using JsonDocument requestBody = JsonDocument.Parse(
            handler.RequestBody!);
        string userPrompt = requestBody.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("[导演叙事]", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_DirectorMemoryDoesNotGroundExternalOpeningStatus()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"城北旧火车站现在通宵开放\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"可以找一个交通方便的室内场所\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiMessageGenerationRequest request = baseRequest with
        {
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    Guid.NewGuid(),
                    baseRequest.Speaker.Id,
                    AiSelfMemoryType.Experience,
                    "上周在城北旧火车站拍过一组雨后照片",
                    "experience.old-station",
                    AiSelfMemoryFactNature.Narrative,
                    AiSelfMemoryMutability.Immutable,
                    AiSelfMemoryTrustLevel.NarrativeCandidate,
                    CharacterWorld.DefaultWorldId,
                    AiSelfMemorySource.Director,
                    80,
                    false,
                    new DateTime(2026, 7, 18),
                    new DateTime(2026, 7, 20))
            }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "可以找一个交通方便的室内场所" }, messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithoutSelfMemoryRejectsDynamicFirstPersonFact()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"我最近正在准备秋季插画展\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"听起来还挺忙的\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(CreateRequest(1));

        Assert.Equal(new[] { "听起来还挺忙的" }, messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_SeparatesReplyTargetFromOlderBackground()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"七点左右\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiDialogueMessage oldMessage = new(
            "我",
            "昨晚看的电影挺安静",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiDialogueMessage targetMessage = new(
            "我",
            "你今天几点回来？",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            FocusContent = targetMessage.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage),
            RecentMessages = new[] { oldMessage, targetMessage },
            ActionPlan = new ConversationActionPlan(
                ConversationAction.Answer,
                ConversationMessageLength.Short,
                ConversationDirectness.Direct,
                ConversationQuestionMode.None,
                ConversationEmotionVisibility.Natural,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Unknown,
                ConversationRelationshipBalance.Unknown,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: true)
        };

        await generator.GenerateMessagesAsync(request);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string userPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        int targetIndex = userPrompt.IndexOf(
            "本轮必须完成的对话目标",
            StringComparison.Ordinal);
        int backgroundIndex = userPrompt.IndexOf(
            "更早的最近消息",
            StringComparison.Ordinal);
        Assert.True(targetIndex >= 0 && targetIndex < backgroundIndex);
        Assert.Contains("你今天几点回来？", userPrompt);
        Assert.Contains("昨晚看的电影挺安静", userPrompt);
        Assert.Equal(1, CountOccurrences(userPrompt, targetMessage.Content));
        Assert.Contains("不要从更早的背景消息中突然恢复旧话题", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_LabelsAiReplyTargetWithoutOwnershipFailure()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"这个方向可以\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiAccount other = new(
            "7654321",
            "小岚",
            string.Empty,
            string.Empty,
            string.Empty);
        AiDialogueMessage targetMessage = new(
            other.Nickname,
            "改成室内拍摄会稳一点",
            MessageSenderType.AiAccount,
            other.Id,
            Guid.NewGuid());
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            OtherParticipants = new[] { other },
            RelationshipTarget = other,
            FocusContent = targetMessage.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(targetMessage),
            RecentMessages = new[] { targetMessage }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "这个方向可以" }, messages);
        using JsonDocument requestBody = JsonDocument.Parse(
            handler.RequestBody!);
        string userPrompt = requestBody.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("本轮具体回应对象“小岚”说过", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenSpeakerBorrowsAnotherExperience_Retries()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"我昨晚也去了蓝桉剧场\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"听着还挺安静的\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiAccount other = new(
            "7654321",
            "小岚",
            string.Empty,
            string.Empty,
            string.Empty);
        AiMessageGenerationRequest request = baseRequest with
        {
            OtherParticipants = new[] { other },
            RecentMessages = new[]
            {
                new AiDialogueMessage(
                    other.Nickname,
                    "我昨晚独自去了蓝桉剧场看零点场",
                    MessageSenderType.AiAccount,
                    other.Id)
            },
            ActionPlan = new ConversationActionPlan(
                ConversationAction.React,
                ConversationMessageLength.Short,
                ConversationDirectness.Partial,
                ConversationQuestionMode.None,
                ConversationEmotionVisibility.Natural,
                ConversationTopicMovement.Stay,
                ConversationPunctuationRhythm.Natural,
                ConversationRelationshipTone.Unknown,
                ConversationRelationshipBalance.Unknown,
                MayOmitObviousContext: true,
                MayLeaveThoughtOpen: true)
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "听着还挺安静的" }, messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenThirdPartyTraitMovesToSpeaker_Retries()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"冷幽默这东西，我基本只对熟人露出来\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"那个咖啡馆老板可能是熟了才会露出来吧\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        DateTime targetSentAt = new(2026, 7, 22, 20, 0, 0);
        AiDialogueMessage oldMessage = new(
            baseRequest.Speaker.Nickname,
            "那个咖啡馆老板偶尔会冒出一些冷幽默",
            MessageSenderType.AiAccount,
            baseRequest.Speaker.Id,
            Guid.NewGuid(),
            targetSentAt.AddDays(-2));
        AiDialogueMessage target = new(
            "我",
            "他是熟了才会这样，还是对谁都这样？",
            MessageSenderType.User,
            null,
            Guid.NewGuid(),
            targetSentAt);
        ConversationReferencePlan referencePlan = new(
            ConversationReferenceStatus.Resolved,
            "他指此前谈到的咖啡馆老板",
            new[] { "冷幽默是咖啡馆老板的特点，不属于当前发言者" });
        ConversationDirectionPlan directionPlan = new(
            baseRequest.ActionPlan!,
            ConversationBeat.Clarify,
            "咖啡馆老板的冷幽默",
            "回应老板对熟人和陌生人的表现差别",
            target.MessageId,
            Array.Empty<string>(),
            new[] { "回答老板是否对谁都这样" },
            "说明这是对第三方特点的推测",
            Array.Empty<string>(),
            Array.Empty<string>(),
            usedRuleFallback: false,
            selectedMessageCount: 1,
            referencePlan: referencePlan);
        AiMessageGenerationRequest request = baseRequest with
        {
            FocusContent = target.Content,
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(target),
            RecentMessages = new[] { oldMessage, target },
            DirectionPlan = directionPlan
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "那个咖啡馆老板可能是熟了才会露出来吧" },
            messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string retryPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("改变了已解析指代的事实归属", retryPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenReplyEchoesOtherSpeaker_AllowsDirectResponse()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"没错，安静看展比挤着舒服多了\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"我更在意展馆离地铁近不近\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            RecentMessages = new[]
            {
                new AiDialogueMessage(
                    "小岚",
                    "安静看展比挤着舒服多了",
                    MessageSenderType.AiAccount,
                    Guid.NewGuid())
            }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "没错，安静看展比挤着舒服多了" }, messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenOutputInventsOmittedSubjectPastExperience_Retries()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"上次雨天去美术馆，几乎包场\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"雨天人少的话确实挺安静\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(CreateRequest(1));

        Assert.Equal(new[] { "雨天人少的话确实挺安静" }, messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_AutonomousGroupTopicSupportsSharedFuturePlan()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"我计划明天改到室内拍摄\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Scenario = AiMessageGenerationScenario.AutonomousGroupChat,
            Topic = "明天降温，把户外拍摄改到室内",
            FocusContent = "明天降温，把户外拍摄改到室内"
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "我计划明天改到室内拍摄" }, messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithGroupPlan_KeepsFactBoundaryAbovePlanDetails()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"我会从材质角度补充。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Scenario = AiMessageGenerationScenario.GroupPrimaryReply,
            GroupConversationPlan = new GroupConversationSpeakerPlan
            {
                SpeakerAiAccountId = Guid.NewGuid(),
                Audience = GroupConversationAudience.LocalUser,
                Role = GroupConversationRole.Complement,
                ResponseGoal = "补充不同观察角度",
                NewContribution = "用没有资料依据的具体藏品举例"
            }
        };

        await generator.GenerateMessagesAsync(request);

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
        Assert.Contains("不是新的事实来源", systemPrompt);
        Assert.Contains("事实边界始终优先", systemPrompt);
        Assert.Contains("群聊分工不是新的事实来源", userPrompt);
        Assert.Contains("必须省略或抽象化", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_CurrentTopicObservationIsNotPersonalHistory()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"最近降温，改成室内拍摄更合适\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(CreateRequest(1));

        Assert.Equal(new[] { "最近降温，改成室内拍摄更合适" }, messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenAutonomousOpeningDrifts_RetriesOnTopic()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"周末去咖啡馆看看吧\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"降温的话，改成室内拍摄更稳\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Scenario = AiMessageGenerationScenario.AutonomousGroupChat,
            Topic = "降温后把户外拍摄改到室内",
            FocusContent = "降温后把户外拍摄改到室内",
            ReplyTarget = AiDialogueReplyTarget.OpenTopic(),
            GroupConversationPlan = new GroupConversationSpeakerPlan
            {
                SpeakerAiAccountId = Guid.NewGuid(),
                Audience = GroupConversationAudience.WholeGroup,
                Role = GroupConversationRole.ShiftTopic,
                ResponseGoal = "自然讨论拍摄地点",
                NewContribution = "提出改到室内拍摄"
            }
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "降温的话，改成室内拍摄更稳" }, messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument requestBody = JsonDocument.Parse(
            handler.RequestBody!);
        string retryPrompt = requestBody.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("自主群聊开场硬约束", retryPrompt);
        Assert.Contains("降温后把户外拍摄改到室内", retryPrompt);
        Assert.Contains("不得改聊账号兴趣", retryPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_UserTopicDoesNotBecomeSpeakerExperience()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"我计划明天改到室内拍摄\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Scenario = AiMessageGenerationScenario.UserPrivateChat,
            Topic = "明天降温，把户外拍摄改到室内",
            FocusContent = "明天降温，把户外拍摄改到室内"
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "这件事我先不乱下结论。" }, messages);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenSilentParticipantIsGivenAnOpinion_Retries()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"看来咱俩都不想雨天去\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"那我先不去了\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest request = CreateRequest(1) with
        {
            Scenario = AiMessageGenerationScenario.AutonomousPrivateChatClosing,
            OtherParticipantHasResponded = false,
            ActionPlan = new ConversationActionPlanner(
                new ConstantRandom(0.2)).CreatePlan(
                    CreateRequest(1),
                    ConversationAction.Close)
        };

        IReadOnlyList<string> messages = await generator
            .GenerateMessagesAsync(request);

        Assert.Equal(new[] { "那我先不去了" }, messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithWorldContext_UsesOnlyRecalledKnowledge()
    {
        RecordingHandler handler = new(CreateResponse(
            HttpStatusCode.OK,
            "{\"messages\":[\"原来那所高中一直受沙漠化影响。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(handler);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        CharacterWorld targetWorld = new(
            "基沃托斯",
            "由多个学院自治区组成的学园都市。");
        AiAccount target = new(
            "7654321",
            "小白",
            "阿拜多斯高中的学生",
            "冷静",
            "简短",
            string.Empty,
            null,
            AiAccountGender.Unspecified,
            "阿拜多斯",
            "学生",
            "基沃托斯",
            OnlineStatus.Online);
        target.AssignCharacterWorld(targetWorld);
        AiConversationWorldKnowledge recalled = new(
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
                new[] { recalled })
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Single(messages);
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
        Assert.Contains("AnomalyObserved", userPrompt);
        Assert.Contains("阿拜多斯是一所受到沙漠化影响的高中", userPrompt);
        Assert.DoesNotContain("所属角色世界：基沃托斯", systemPrompt);
        Assert.DoesNotContain("对方世界可称为“基沃托斯”", userPrompt);
        Assert.DoesNotContain("跨世界远程通信对象：小白", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WithUnauthorizedForeignTerm_Retries()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"阿拜多斯那边应该很干燥吧。\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"那个地方听着有点陌生。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        CharacterWorld targetWorld = new(
            "基沃托斯",
            "由多个学院自治区组成的学园都市。");
        AiAccount target = new(
            "7654321",
            "小白",
            "阿拜多斯高中的学生",
            "冷静",
            "简短",
            string.Empty,
            null,
            AiAccountGender.Unspecified,
            "阿拜多斯",
            "学生",
            "基沃托斯",
            OnlineStatus.Online);
        target.AssignCharacterWorld(targetWorld);
        AiMessageGenerationRequest request = baseRequest with
        {
            Scenario = AiMessageGenerationScenario.AutonomousPrivateChat,
            OtherParticipants = new[] { target },
            RelationshipTarget = target,
            WorldConversationContext = new AiWorldConversationContext(
                AiParallelWorldAwarenessState.Unaware,
                AiWorldAwarenessState.AssumedSharedWorld,
                target.Id,
                targetWorld.Id,
                VisibleSubjectWorldName: null,
                IsNewlyInformedByCurrentMessage: false,
                AiWorldInquiryMode.None,
                Array.Empty<AiConversationWorldKnowledge>())
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(new[] { "那个地方听着有点陌生。" }, messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument retryRequest = JsonDocument.Parse(
            handler.RequestBody!);
        string retryPrompt = retryRequest.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("尚未从对话中获知“阿拜多斯”", retryPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_GroupContext_RejectsAnotherMembersUnlearnedWorldFact()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"阿拜多斯那边应该很干燥吧。\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"你刚才说的地方听起来挺特别。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        CharacterWorld targetWorld = new(
            "基沃托斯",
            "由多个学院自治区组成的学园都市。");
        AiAccount target = new(
            "7654321",
            "小白",
            "阿拜多斯高中的学生",
            "冷静",
            "简短",
            string.Empty,
            null,
            AiAccountGender.Unspecified,
            "阿拜多斯",
            "学生",
            "基沃托斯",
            OnlineStatus.Online);
        target.AssignCharacterWorld(targetWorld);
        AiWorldConversationContext targetContext = new(
            AiParallelWorldAwarenessState.Unaware,
            AiWorldAwarenessState.AssumedSharedWorld,
            target.Id,
            targetWorld.Id,
            VisibleSubjectWorldName: null,
            IsNewlyInformedByCurrentMessage: false,
            AiWorldInquiryMode.None,
            Array.Empty<AiConversationWorldKnowledge>());
        AiMessageGenerationRequest request = baseRequest with
        {
            Scenario = AiMessageGenerationScenario.GroupPrimaryReply,
            OtherParticipants = new[] { target },
            RelationshipTarget = null,
            WorldConversationContext = null,
            GroupWorldConversationContext =
                new AiGroupWorldConversationContext(
                    AiParallelWorldAwarenessState.Unaware,
                    IsNewlyInformedByCurrentMessage: false,
                    new[] { targetContext })
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "你刚才说的地方听起来挺特别。" },
            messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument retryRequest = JsonDocument.Parse(
            handler.RequestBody!);
        string userPrompt = retryRequest.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("逐成员隔离", userPrompt);
        Assert.Contains("尚未从对话中获知“阿拜多斯”", userPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_BeforeConfirmation_RejectsCrossWorldConclusion()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"原来我们不在同一个世界。\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"你那边的生活听着确实不太一样。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        CharacterWorld targetWorld = new(
            "远方世界",
            "拥有不同生活规则的地区。");
        AiAccount target = new(
            "7654321",
            "远方好友",
            string.Empty,
            "谨慎",
            "简短");
        target.AssignCharacterWorld(targetWorld);
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
                Array.Empty<AiConversationWorldKnowledge>())
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "你那边的生活听着确实不太一样。" },
            messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenUserClaimConflictsWithProtectedFact_RetriesWithCanon()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"是啊，导师就是在阿拜多斯沙暴中失踪的\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"不是阿拜多斯。我的导师是在一次大回潮中失踪的\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiDialogueMessage userClaim = new(
            "我",
            "你的导师是在阿拜多斯沙暴中失踪的，对吧？",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = baseRequest with
        {
            FocusContent = userClaim.Content,
            RecentMessages = new[] { userClaim },
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(userClaim),
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    Guid.NewGuid(),
                    baseRequest.Speaker.Id,
                    AiSelfMemoryType.Experience,
                    "我的导师在一次大回潮中失踪，这件事至今没有确定结论",
                    "identity.mentor-disappearance",
                    AiSelfMemoryFactNature.Objective,
                    AiSelfMemoryMutability.Immutable,
                    AiSelfMemoryTrustLevel.UserCanon,
                    CharacterWorld.DefaultWorldId,
                    AiSelfMemorySource.User,
                    100,
                    true,
                    null,
                    new DateTime(2026, 7, 24))
            }
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "不是阿拜多斯。我的导师是在一次大回潮中失踪的" },
            messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument protectedFactInitial = JsonDocument.Parse(
            Assert.IsType<string>(handler.RequestBodies[0]));
        string protectedFactInitialPrompt = protectedFactInitial.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains(
            "回答只能使用上方受保护条目",
            protectedFactInitialPrompt);
        using JsonDocument protectedFactRetry = JsonDocument.Parse(
            Assert.IsType<string>(handler.RequestBodies[1]));
        string protectedFactRetryPrompt = protectedFactRetry.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains(
            "受保护事实不能被近期说法覆盖",
            protectedFactRetryPrompt);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenCanonReplyUsesConfirmationWording_DoesNotRejectItAsConflict()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"我能确认的是，导师是在一次大回潮中失踪的。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiDialogueMessage userClaim = new(
            "我",
            "关于导师失踪，只说你已经确认的事实：他是在阿拜多斯沙暴里失踪的吗？",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = baseRequest with
        {
            FocusContent = userClaim.Content,
            RecentMessages = new[] { userClaim },
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(userClaim),
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    Guid.NewGuid(),
                    baseRequest.Speaker.Id,
                    AiSelfMemoryType.Experience,
                    "我的导师在一次大回潮中失踪，因此我面对异常航路时会先寻找可靠证据",
                    "identity.mentor-disappearance",
                    AiSelfMemoryFactNature.Objective,
                    AiSelfMemoryMutability.Immutable,
                    AiSelfMemoryTrustLevel.UserCanon,
                    CharacterWorld.DefaultWorldId,
                    AiSelfMemorySource.User,
                    100,
                    true,
                    null,
                    new DateTime(2026, 7, 24))
            }
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "我能确认的是，导师是在一次大回潮中失踪的。" },
            messages);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenExplicitCanonRecallIsEvaded_RetriesWithFact()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"这件事我先不乱下结论。\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"我能确认的是，导师是在一次大回潮中失踪的。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiDialogueMessage userClaim = new(
            "我",
            "请纠正我：导师失踪和阿拜多斯沙暴有关吗？只按你确定的记忆说。",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = baseRequest with
        {
            FocusContent = userClaim.Content,
            RecentMessages = new[] { userClaim },
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(userClaim),
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    Guid.NewGuid(),
                    baseRequest.Speaker.Id,
                    AiSelfMemoryType.Experience,
                    "我的导师在一次大回潮中失踪",
                    "identity.mentor-disappearance",
                    AiSelfMemoryFactNature.Objective,
                    AiSelfMemoryMutability.Immutable,
                    AiSelfMemoryTrustLevel.UserCanon,
                    CharacterWorld.DefaultWorldId,
                    AiSelfMemorySource.User,
                    100,
                    true,
                    null,
                    new DateTime(2026, 7, 24))
            }
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "我能确认的是，导师是在一次大回潮中失踪的。" },
            messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenProtectedCausalityIsReversed_RetriesWithoutInversion()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"异常航路引发了大回潮，导师因此失踪。\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"导师在一次大回潮中失踪，所以我面对异常航路时会先找证据。\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiDialogueMessage userClaim = new(
            "我",
            "请按你确定的事实说说导师失踪的事。",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = baseRequest with
        {
            FocusContent = userClaim.Content,
            RecentMessages = new[] { userClaim },
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(userClaim),
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    Guid.NewGuid(),
                    baseRequest.Speaker.Id,
                    AiSelfMemoryType.Experience,
                    "我的导师在一次大回潮中失踪，因此我面对异常航路时会先寻找可靠证据",
                    "identity.mentor-disappearance",
                    AiSelfMemoryFactNature.Objective,
                    AiSelfMemoryMutability.Immutable,
                    AiSelfMemoryTrustLevel.UserCanon,
                    CharacterWorld.DefaultWorldId,
                    AiSelfMemorySource.User,
                    100,
                    true,
                    null,
                    new DateTime(2026, 7, 24))
            }
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "导师在一次大回潮中失踪，所以我面对异常航路时会先找证据。" },
            messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenProtectedFactRepliesRemainInvalid_FallsBackToCanon()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"是啊，导师是在阿拜多斯沙暴中失踪的\","
                + "\"这就是我记得的版本\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"导师确实是在阿拜多斯沙暴中失踪的\","
                + "\"这件事没有别的说法\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(2);
        AiDialogueMessage userClaim = new(
            "我",
            "你的导师是在阿拜多斯沙暴中失踪的，对吧？",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = baseRequest with
        {
            FocusContent = userClaim.Content,
            RecentMessages = new[] { userClaim },
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(userClaim),
            RelevantSelfMemories = new[]
            {
                new AiConversationSelfMemory(
                    Guid.NewGuid(),
                    baseRequest.Speaker.Id,
                    AiSelfMemoryType.Experience,
                    "我的导师在一次大回潮中失踪",
                    "identity.mentor-disappearance",
                    AiSelfMemoryFactNature.Objective,
                    AiSelfMemoryMutability.Immutable,
                    AiSelfMemoryTrustLevel.UserCanon,
                    CharacterWorld.DefaultWorldId,
                    AiSelfMemorySource.User,
                    100,
                    true,
                    null,
                    new DateTime(2026, 7, 24))
            }
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "我能确认的是，我的导师在一次大回潮中失踪。" },
            messages);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateMessagesAsync_WhenUserAssignsThirdPartyTaskToSpeaker_RetriesWithoutTakingOwnership()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"我确实负责了阿拜多斯仓库的物资清点\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"仓库清点是星野负责的部分，我没有参与\"]}"));
        OpenAiCompatibleAiMessageGenerator generator = CreateGenerator(
            handler,
            outputValidationRetryCount: 1);
        AiMessageGenerationRequest baseRequest = CreateRequest(1);
        AiDialogueMessage userClaim = new(
            "我",
            "你亲自在阿拜多斯仓库清点了救援物资",
            MessageSenderType.User,
            null,
            Guid.NewGuid());
        AiMessageGenerationRequest request = baseRequest with
        {
            FocusContent = userClaim.Content,
            RecentMessages = new[] { userClaim },
            ReplyTarget = AiDialogueReplyTarget.ReplyTo(userClaim)
        };

        IReadOnlyList<string> messages =
            await generator.GenerateMessagesAsync(request);

        Assert.Equal(
            new[] { "仓库清点是星野负责的部分，我没有参与" },
            messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument ownershipRetry = JsonDocument.Parse(
            Assert.IsType<string>(handler.RequestBodies[1]));
        string ownershipRetryPrompt = ownershipRetry.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains(
            "不能把其他参与者的具体经历",
            ownershipRetryPrompt);
    }

    private static OpenAiCompatibleAiMessageGenerator CreateGenerator(
        HttpMessageHandler handler,
        int outputValidationRetryCount = 0,
        AiInteractionDiagnosticLogService? diagnosticLogService = null)
    {
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("http://localhost:11434/v1/")
        };
        AiMessageGenerationOptions options = new()
            {
                BaseUrl = "https://api.example.test/v1/",
                Model = "vocachat-test-model",
                OutputValidationRetryCount = outputValidationRetryCount
            };
        return new OpenAiCompatibleAiMessageGenerator(
            new OpenAiCompatibleChatClient(client, options),
            options,
            new AiConversationContextBuilder(),
            diagnosticLogService);
    }

    private static AiMessageGenerationRequest CreateRequest(
        int expectedMessageCount)
    {
        AiMessageGenerationRequest request = new()
        {
            Scenario = AiMessageGenerationScenario.UserPrivateChat,
            Speaker = new AiAccount(
                "1234567",
                "小语",
                "喜欢安静聊天的朋友",
                "温和",
                "简短自然"),
            FocusContent = "晚上好",
            RecentMessages = new[]
            {
                new AiDialogueMessage(
                    "我",
                    "晚上好",
                    MessageSenderType.User,
                    null)
            },
            ExpectedMessageCount = expectedMessageCount
        };
        return new ConversationActionPlanner(new ConstantRandom(0.2))
            .ApplyPlan(request);
    }

    private static HttpResponseMessage CreateResponse(
        HttpStatusCode statusCode,
        string generatedContent)
    {
        string responseBody = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = generatedContent
                    }
                }
            }
        });
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                responseBody,
                Encoding.UTF8,
                "application/json")
        };
    }

    private static int CountOccurrences(string value, string searchValue)
    {
        int count = 0;
        int searchIndex = 0;

        while ((searchIndex = value.IndexOf(
                   searchValue,
                   searchIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += searchValue.Length;
        }

        return count;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public string? RequestBody { get; private set; }
        public List<string?> RequestBodies { get; } = new();
        public int CallCount { get; private set; }

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(RequestBody);
            return _responses.Dequeue();
        }
    }

    private sealed class ConstantRandom : Random
    {
        private readonly double _value;

        public ConstantRandom(double value)
        {
            _value = value;
        }

        protected override double Sample()
        {
            return _value;
        }
    }
}

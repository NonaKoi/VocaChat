using System.Net;
using System.Text;
using System.Text.Json;
using VocaChat.Models;
using VocaChat.Services;

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
    public async Task GenerateMessagesAsync_WhenMessageExceedsPlannedLength_RetriesWithReason()
    {
        RecordingHandler handler = new(
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"这是一条明显超过当前短消息表达计划限制而且过度完整的回复内容\"]}"),
            CreateResponse(
                HttpStatusCode.OK,
                "{\"messages\":[\"先歇会儿吧\"]}"));
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

        Assert.Equal(new[] { "先歇会儿吧" }, messages);
        Assert.Equal(2, handler.CallCount);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
        string retryPrompt = body.RootElement.GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("不超过 18 个字符", retryPrompt);
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
            "[本人过去说过，可延续为自己的历史]",
            userPrompt);
        Assert.Contains(
            "[其他好友“小岚”说过，只能视为听到的内容]",
            userPrompt);
        Assert.Contains(
            "[本地用户说过，只能视为用户的信息]",
            userPrompt);
        Assert.Contains("我昨晚去杭州看演出", userPrompt);
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
    public async Task GenerateMessagesAsync_WhenOutputSemanticallyRepeatsRecentMessage_Retries()
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

        Assert.Equal(new[] { "我更在意展馆离地铁近不近" }, messages);
        Assert.Equal(2, handler.CallCount);
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

    private static OpenAiCompatibleAiMessageGenerator CreateGenerator(
        HttpMessageHandler handler,
        int outputValidationRetryCount = 0)
    {
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("http://localhost:11434/v1/")
        };
        AiMessageGenerationOptions options = new()
            {
                Model = "vocachat-test-model",
                OutputValidationRetryCount = outputValidationRetryCount
            };
        return new OpenAiCompatibleAiMessageGenerator(
            new OpenAiCompatibleChatClient(client, options),
            options,
            new AiConversationContextBuilder());
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

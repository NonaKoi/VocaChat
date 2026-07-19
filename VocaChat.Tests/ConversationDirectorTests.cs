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
        int messageCount = 1) =>
        JsonSerializer.Serialize(new
        {
            action,
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
            forbiddenClaims = new[] { "没有本人历史依据的昨晚行程" }
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

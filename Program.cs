using Microsoft.EntityFrameworkCore;
using VocaChat.ConsoleApp.ConsoleUi;
using VocaChat.Data;
using VocaChat.Services;

namespace VocaChat.ConsoleApp;

public class Program
{
    /// <summary>
    /// 程序入口：更新数据库结构、创建业务 Service，并启动控制台应用。
    /// </summary>
    public static async Task Main(string[] args)
    {
        VocaChatDbContextFactory dbContextFactory = new();

        using (VocaChatDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Database.Migrate();
        }

        AiAccountService aiAccountService = new(dbContextFactory);
        GroupChatService groupChatService = new(dbContextFactory);
        GroupMessageService groupMessageService = new(dbContextFactory);
        GroupChatReplyPlanner replyPlanner = new(dbContextFactory);
        ConversationActionPlanner actionPlanner = new();
        AiMessageGenerationOptions messageGenerationOptions = new()
        {
            BaseUrl = Environment.GetEnvironmentVariable("VOCACHAT_AI_BASE_URL")
                ?? "http://127.0.0.1:11434/v1/",
            Model = Environment.GetEnvironmentVariable("VOCACHAT_AI_MODEL")
                ?? "vocachat-qwen3.5-4b",
            ApiKey = Environment.GetEnvironmentVariable("VOCACHAT_AI_API_KEY")
        };
        AiModelConnectionSettingsService modelConnectionSettingsService = new(
            dbContextFactory,
            messageGenerationOptions,
            new AiApiKeyProtector());
        using HttpClient modelClient = new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        AiModelInvocationUsageService modelUsageService = new(
            dbContextFactory);
        OpenAiCompatibleChatClient chatClient = new(
            modelClient,
            messageGenerationOptions,
            modelConnectionSettingsService,
            modelUsageService);
        IAiMessageGenerator messageGenerator =
            new OpenAiCompatibleAiMessageGenerator(
                chatClient,
                messageGenerationOptions,
                new AiConversationContextBuilder());
        IConversationDirector conversationDirector =
            new OpenAiCompatibleConversationDirector(
                chatClient,
                messageGenerationOptions,
                new AiConversationContextBuilder(),
                actionPlanner);
        AiReplyTimingScheduler replyTimingScheduler = new(dbContextFactory);
        AiReplyMessageCountSettingsResolver replyMessageCountSettingsResolver =
            new(dbContextFactory);
        ConversationQuestionPolicyService questionPolicyService = new(
            dbContextFactory);
        AiInteractionDiagnosticLogService diagnosticLogService = new(
            dbContextFactory);
        AiIdentityContinuityService identityContinuityService = new(
            new AiSelfMemoryService(dbContextFactory),
            diagnosticLogService);
        GroupConversationContextService conversationContextService = new(
            dbContextFactory,
            identityContinuityService);
        GroupConversationPlanValidator groupPlanValidator = new();
        GroupConversationDensitySettingsResolver densityResolver = new(
            dbContextFactory);
        GroupConversationDiagnosticService groupDiagnosticService = new(
            diagnosticLogService);
        IGroupConversationDirector groupConversationDirector =
            new OpenAiCompatibleGroupConversationDirector(
                chatClient,
                messageGenerationOptions,
                replyPlanner,
                groupPlanValidator,
                conversationContextService);
        GroupChatInteractionService interactionService = new(
            groupMessageService,
            messageGenerator,
            replyPlanner,
            groupConversationDirector,
            groupPlanValidator,
            conversationDirector,
            replyTimingScheduler,
            replyMessageCountSettingsResolver,
            questionPolicyService,
            identityContinuityService,
            conversationContextService,
            densityResolver,
            groupDiagnosticService);

        VocaChatConsoleApp consoleApp = new(
            aiAccountService,
            groupChatService,
            groupMessageService,
            interactionService);

        await consoleApp.RunAsync();
    }
}

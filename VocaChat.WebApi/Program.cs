using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Services;
using VocaChat.WebApi.Services;

namespace VocaChat.WebApi;

public class Program
{
    /// <summary>
    /// 配置并启动 VocaChat Web API Host。
    /// </summary>
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        AiMessageGenerationOptions messageGenerationOptions =
            builder.Configuration
                .GetSection(AiMessageGenerationOptions.SectionName)
                .Get<AiMessageGenerationOptions>()
            ?? new AiMessageGenerationOptions();
        messageGenerationOptions.BaseUrl =
            Environment.GetEnvironmentVariable("VOCACHAT_AI_BASE_URL")
            ?? messageGenerationOptions.BaseUrl;
        messageGenerationOptions.Model =
            Environment.GetEnvironmentVariable("VOCACHAT_AI_MODEL")
            ?? messageGenerationOptions.Model;
        messageGenerationOptions.ApiKey =
            Environment.GetEnvironmentVariable("VOCACHAT_AI_API_KEY")
            ?? messageGenerationOptions.ApiKey;
        builder.Services.AddSingleton(messageGenerationOptions);
        builder.Services.AddSingleton<AiConversationContextBuilder>();
        builder.Services.AddHttpClient<OpenAiCompatibleChatClient>(client =>
        {
            string baseUrl = messageGenerationOptions.BaseUrl.EndsWith('/')
                ? messageGenerationOptions.BaseUrl
                : $"{messageGenerationOptions.BaseUrl}/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        builder.Services.AddScoped<
            IAiMessageGenerator,
            OpenAiCompatibleAiMessageGenerator>();

        builder.Services.AddSingleton<VocaChatDbContextFactory>();
        builder.Services.AddScoped<AiAccountService>();
        builder.Services.AddScoped<ContactService>();
        builder.Services.AddScoped<PrivateChatService>();
        builder.Services.AddScoped<ConversationActionPlanner>();
        builder.Services.AddScoped<
            IConversationDirector,
            OpenAiCompatibleConversationDirector>();
        builder.Services.AddScoped<PrivateChatInteractionService>();
        builder.Services.AddScoped<ConversationService>();
        builder.Services.AddScoped<AutonomousInteractionSettingsService>();
        builder.Services.AddScoped<AiAccountAutonomySettingsService>();
        builder.Services.AddScoped<AiRelationshipService>();
        builder.Services.AddScoped<RelationshipEvolutionService>();
        builder.Services.AddScoped<AutonomousPrivateChatJudge>();
        builder.Services.AddScoped<AutonomousPrivateChatPlanningService>();
        builder.Services.AddScoped<AutonomousPrivateChatSessionService>();
        builder.Services.AddScoped<AutonomousPrivateChatRoundPlanner>();
        builder.Services.AddScoped<AutonomousPrivateChatContinuationDecider>();
        builder.Services.AddScoped<AutonomousPrivateChatClosurePlanner>();
        builder.Services.AddScoped<AutonomousPrivateChatRandomSource>();
        builder.Services.AddScoped<AutonomousPrivateChatExecutionService>();
        builder.Services.AddScoped<GroupChatService>();
        builder.Services.AddScoped<GroupMessageService>();
        builder.Services.AddScoped<GroupChatReplyPlanner>();
        builder.Services.AddScoped<GroupChatInteractionService>();
        builder.Services.AddSingleton(_ =>
            new LocalMediaStorageService(
                VocaChatApplicationPaths.GetMediaDirectory()));
        builder.Services.AddScoped<AiAccountMediaService>();
        builder.Services.AddScoped<PostService>();
        builder.Services.AddScoped<PostMediaService>();

        WebApplication app = builder.Build();

        ApplyDatabaseMigrations(app);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.MapControllers();

        app.Run();
    }

    /// <summary>
    /// Web API 启动时使用现有数据库工厂应用 Shared 中的 Migration。
    /// </summary>
    private static void ApplyDatabaseMigrations(WebApplication app)
    {
        VocaChatDbContextFactory dbContextFactory =
            app.Services.GetRequiredService<VocaChatDbContextFactory>();

        using VocaChatDbContext dbContext = dbContextFactory.CreateDbContext();
        dbContext.Database.Migrate();
    }
}

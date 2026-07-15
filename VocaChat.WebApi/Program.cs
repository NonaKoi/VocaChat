using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Services;

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

        builder.Services.AddSingleton<VocaChatDbContextFactory>();
        builder.Services.AddScoped<AiAccountService>();
        builder.Services.AddScoped<GroupChatService>();
        builder.Services.AddScoped<GroupMessageService>();
        builder.Services.AddSingleton<FakeAiReplyService>();

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

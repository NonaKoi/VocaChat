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
    public static void Main(string[] args)
    {
        VocaChatDbContextFactory dbContextFactory = new();

        using (VocaChatDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.Database.Migrate();
        }

        AiAccountService aiAccountService = new(dbContextFactory);
        GroupChatService groupChatService = new(dbContextFactory);
        GroupMessageService groupMessageService = new(dbContextFactory);
        FakeAiReplyService fakeAiReplyService = new();

        VocaChatConsoleApp consoleApp = new(
            aiAccountService,
            groupChatService,
            groupMessageService,
            fakeAiReplyService);

        consoleApp.Run();
    }
}

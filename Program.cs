using VocaChat.ConsoleApp.ConsoleUi;
using VocaChat.ConsoleApp.Services;

namespace VocaChat.ConsoleApp;

public class Program
{
    /// <summary>
    /// 程序入口：创建业务 Service，并启动控制台应用。
    /// </summary>
    public static void Main(string[] args)
    {
        AiAccountService aiAccountService = new();
        GroupChatService groupChatService = new(aiAccountService);
        GroupMessageService groupMessageService = new(groupChatService);
        FakeAiReplyService fakeAiReplyService = new();

        VocaChatConsoleApp consoleApp = new(
            aiAccountService,
            groupChatService,
            groupMessageService,
            fakeAiReplyService);

        consoleApp.Run();
    }
}

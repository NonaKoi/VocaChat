using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VocaChat.ConsoleApp.Models;
using VocaChat.ConsoleApp.Services;

namespace VocaChat.ConsoleApp;

public class Program
{
    /// <summary>
    /// 程序入口：准备各项内存服务，并控制第一阶段完整 Console 流程的执行顺序。
    /// </summary>
    public static void Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        AiAccountService aiAccountService = new();
        GroupChatService groupChatService = new();
        FakeAiReplyService fakeAiReplyService = new();

        DisplayStartupInformation();

        CreateAiAccounts(aiAccountService);
        DisplayAiAccounts(aiAccountService.Accounts);

        GroupChat groupChat = CreateGroupChat(
            aiAccountService.Accounts,
            groupChatService);
        DisplayGroupChat(groupChat);

        EnterGroupChat(groupChat, groupChatService, fakeAiReplyService);
        DisplayChatHistory(groupChat, groupChatService);
    }

    /// <summary>
    /// 显示当前 Console 原型的阶段和假回复说明。
    /// </summary>
    private static void DisplayStartupInformation()
    {
        Console.WriteLine("VocaChat Console 原型");
        Console.WriteLine("当前处于第一阶段");
        Console.WriteLine("当前 AI 回复由本地规则模拟，不会调用真实 AI API。");
        Console.WriteLine();
    }

    /// <summary>
    /// 循环获取账号资料，并调用账号服务完成验证、创建和内存保存。
    /// </summary>
    private static void CreateAiAccounts(AiAccountService aiAccountService)
    {
        while (true)
        {
            Console.Write("请输入 AI 账号昵称，或输入 /done 结束创建：");
            string nicknameInput = ReadRequiredLine();
            string nickname = nicknameInput.Trim();

            if (nickname.Equals("/done", StringComparison.OrdinalIgnoreCase))
            {
                if (aiAccountService.Accounts.Count == 0)
                {
                    Console.WriteLine("至少需要创建一个 AI 账号后才能结束。");
                    continue;
                }

                break;
            }

            string? validationError = aiAccountService.ValidateNickname(nickname);

            if (validationError is not null)
            {
                Console.WriteLine(validationError);
                continue;
            }

            Console.Write("请输入身份描述（可留空）：");
            string identityDescription = ReadRequiredLine();

            Console.Write("请输入性格（可留空）：");
            string personality = ReadRequiredLine();

            Console.Write("请输入说话风格（可留空）：");
            string speakingStyle = ReadRequiredLine();

            AiAccount aiAccount = aiAccountService.CreateAiAccount(
                nickname,
                identityDescription,
                personality,
                speakingStyle);

            Console.WriteLine($"已创建 AI 账号：{aiAccount.Nickname}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 按创建顺序显示当前内存中保存的全部 AI 账号。
    /// </summary>
    private static void DisplayAiAccounts(IReadOnlyList<AiAccount> aiAccounts)
    {
        Console.WriteLine();
        Console.WriteLine("已创建的 AI 账号：");

        for (int i = 0; i < aiAccounts.Count; i++)
        {
            AiAccount account = aiAccounts[i];

            Console.WriteLine($"{i + 1}. {account.Nickname}");
            Console.WriteLine($"   身份描述：{DisplayText(account.IdentityDescription)}");
            Console.WriteLine($"   性格：{DisplayText(account.Personality)}");
            Console.WriteLine($"   说话风格：{DisplayText(account.SpeakingStyle)}");
            Console.WriteLine($"   创建时间：{account.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }
    }

    /// <summary>
    /// 获取群聊名称和成员编号，并调用群聊服务创建群聊。
    /// </summary>
    private static GroupChat CreateGroupChat(
        IReadOnlyList<AiAccount> aiAccounts,
        GroupChatService groupChatService)
    {
        Console.WriteLine();
        Console.WriteLine("开始创建群聊。");

        string groupName = ReadGroupChatName(groupChatService);

        Console.WriteLine();
        Console.WriteLine("请选择要加入群聊的 AI 账号。");
        DisplayAiAccountOptions(aiAccounts);

        List<AiAccount> selectedMembers = SelectGroupMembers(aiAccounts);
        return groupChatService.CreateGroupChat(groupName, selectedMembers);
    }

    /// <summary>
    /// 循环读取群聊名称，并通过群聊服务完成名称验证。
    /// </summary>
    private static string ReadGroupChatName(GroupChatService groupChatService)
    {
        while (true)
        {
            Console.Write("请输入群聊名称：");
            string groupName = ReadRequiredLine().Trim();
            string? validationError = groupChatService.ValidateGroupChatName(groupName);

            if (validationError is null)
            {
                return groupName;
            }

            Console.WriteLine(validationError);
        }
    }

    /// <summary>
    /// 按编号显示可加入群聊的 AI 账号。
    /// </summary>
    private static void DisplayAiAccountOptions(IReadOnlyList<AiAccount> aiAccounts)
    {
        for (int i = 0; i < aiAccounts.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {aiAccounts[i].Nickname}");
        }
    }

    /// <summary>
    /// 循环读取成员编号，直到用户至少选择一个有效 AI 账号。
    /// </summary>
    private static List<AiAccount> SelectGroupMembers(IReadOnlyList<AiAccount> aiAccounts)
    {
        while (true)
        {
            Console.Write("请输入成员编号，多个编号用英文逗号分隔，例如 1,3：");
            string input = ReadRequiredLine();

            if (TryGetSelectedMemberIndexes(input, aiAccounts.Count, out List<int> selectedIndexes))
            {
                return selectedIndexes
                    .Select(index => aiAccounts[index])
                    .ToList();
            }
        }
    }

    /// <summary>
    /// 解析用户输入的成员编号；忽略重复编号，并检查每个编号是否存在。
    /// </summary>
    private static bool TryGetSelectedMemberIndexes(
        string input,
        int aiAccountCount,
        out List<int> selectedIndexes)
    {
        selectedIndexes = new List<int>();

        string[] parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            Console.WriteLine("至少需要选择一个 AI 账号。");
            return false;
        }

        HashSet<int> selectedIndexSet = new();

        foreach (string part in parts)
        {
            string trimmedPart = part.Trim();

            if (!int.TryParse(trimmedPart, out int memberNumber))
            {
                Console.WriteLine($"“{trimmedPart}”不是有效编号，请重新输入。");
                return false;
            }

            if (memberNumber < 1 || memberNumber > aiAccountCount)
            {
                Console.WriteLine($"编号 {memberNumber} 不存在，请重新输入。");
                return false;
            }

            selectedIndexSet.Add(memberNumber - 1);
        }

        if (selectedIndexSet.Count == 0)
        {
            Console.WriteLine("至少需要选择一个 AI 账号。");
            return false;
        }

        selectedIndexes = selectedIndexSet.ToList();
        return true;
    }

    /// <summary>
    /// 显示已创建群聊的基本信息和 AI 账号成员列表。
    /// </summary>
    private static void DisplayGroupChat(GroupChat groupChat)
    {
        Console.WriteLine();
        Console.WriteLine("群聊创建成功：");
        Console.WriteLine($"群聊名称：{groupChat.Name}");
        Console.WriteLine($"创建时间：{groupChat.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("群成员：");

        for (int i = 0; i < groupChat.Members.Count; i++)
        {
            AiAccount member = groupChat.Members[i];
            Console.WriteLine($"{i + 1}. {member.Nickname}");
        }
    }

    /// <summary>
    /// 处理聊天输入，并调用服务保存用户消息、选择 AI 和保存假回复。
    /// </summary>
    private static void EnterGroupChat(
        GroupChat groupChat,
        GroupChatService groupChatService,
        FakeAiReplyService fakeAiReplyService)
    {
        Console.WriteLine();
        Console.WriteLine($"进入群聊：{groupChat.Name}");
        Console.WriteLine("输入消息内容发送；输入 /exit 结束聊天。");

        while (true)
        {
            Console.Write("我：");
            string input = ReadRequiredLine();
            string content = input.Trim();

            if (content.Equals("/exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            GroupMessage? userMessage = groupChatService.SaveUserMessage(groupChat, content);

            if (userMessage is null)
            {
                Console.WriteLine("消息内容不能为空，请重新输入。");
                continue;
            }

            DisplayMessage(userMessage);

            AiAccount? aiSpeaker = groupChatService.SelectAiSpeaker(
                groupChat,
                userMessage.Content,
                out bool selectedByMention);

            if (aiSpeaker is null)
            {
                Console.WriteLine("当前群聊没有 AI 成员，无法生成假回复。");
                continue;
            }

            if (userMessage.Content.Contains('@') && !selectedByMention)
            {
                Console.WriteLine("没有找到被点名的群内 AI，将按默认规则选择发言者。");
            }

            string fakeReply = fakeAiReplyService.GenerateReply(aiSpeaker, userMessage.Content);
            GroupMessage aiMessage = groupChatService.SaveAiReply(
                groupChat,
                aiSpeaker,
                fakeReply);

            DisplayMessage(aiMessage);
        }
    }

    /// <summary>
    /// 获取并显示当前群聊按发送时间排列的完整聊天记录。
    /// </summary>
    private static void DisplayChatHistory(
        GroupChat groupChat,
        GroupChatService groupChatService)
    {
        Console.WriteLine();
        Console.WriteLine($"群聊“{groupChat.Name}”的聊天记录：");

        IReadOnlyList<GroupMessage> orderedMessages =
            groupChatService.GetOrderedChatHistory(groupChat);

        if (orderedMessages.Count == 0)
        {
            Console.WriteLine("暂无消息。");
            return;
        }

        foreach (GroupMessage message in orderedMessages)
        {
            DisplayMessage(message);
        }
    }

    /// <summary>
    /// 使用统一格式显示一条群消息的发送时间、发送者名称和内容。
    /// </summary>
    private static void DisplayMessage(GroupMessage message)
    {
        Console.WriteLine(
            $"[{message.SentAt:yyyy-MM-dd HH:mm:ss}] "
            + $"{message.SenderDisplayName}：{message.Content}");
    }

    /// <summary>
    /// 将空白字段统一显示为“未填写”，避免列表中出现空内容。
    /// </summary>
    private static string DisplayText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "（未填写）" : value;
    }

    /// <summary>
    /// 读取一行控制台输入；如果没有读到内容，则返回空字符串。
    /// </summary>
    private static string ReadRequiredLine()
    {
        return Console.ReadLine() ?? string.Empty;
    }
}

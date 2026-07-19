using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.ConsoleApp.ConsoleUi;

/// <summary>
/// 负责 VocaChat Console 原型的输入、输出和完整执行流程。
/// </summary>
public class VocaChatConsoleApp
{
    private readonly AiAccountService _aiAccountService;
    private readonly GroupChatService _groupChatService;
    private readonly GroupMessageService _groupMessageService;
    private readonly GroupChatInteractionService _interactionService;

    /// <summary>
    /// 创建控制台应用，并接收完整流程所需的业务 Service。
    /// </summary>
    public VocaChatConsoleApp(
        AiAccountService aiAccountService,
        GroupChatService groupChatService,
        GroupMessageService groupMessageService,
        GroupChatInteractionService interactionService)
    {
        _aiAccountService = aiAccountService;
        _groupChatService = groupChatService;
        _groupMessageService = groupMessageService;
        _interactionService = interactionService;
    }

    /// <summary>
    /// 按顺序执行账号准备、群聊选择或创建，以及群消息流程。
    /// </summary>
    public async Task RunAsync()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        DisplayStartupInformation();

        CreateAiAccounts();
        IReadOnlyList<AiAccount> aiAccounts = _aiAccountService.GetAllAccounts();
        DisplayAiAccounts(aiAccounts);

        GroupChat groupChat = SelectOrCreateGroupChat(aiAccounts);
        DisplayGroupChat(groupChat);

        DisplayChatHistory(groupChat);
        await EnterGroupChatAsync(groupChat);
        DisplayChatHistory(groupChat);
    }

    /// <summary>
    /// 显示当前 Console 原型的阶段和假回复说明。
    /// </summary>
    private static void DisplayStartupInformation()
    {
        Console.WriteLine("VocaChat Console 原型");
        Console.WriteLine("当前处于第三阶段：SQLite 与 EF Core");
        Console.WriteLine("当前 AI 回复由本地规则模拟，不会调用真实 AI API。");
        Console.WriteLine();
    }

    /// <summary>
    /// 循环获取账号资料，并调用账号 Service 完成验证、创建和数据库保存。
    /// </summary>
    private void CreateAiAccounts()
    {
        while (true)
        {
            Console.Write("请输入 AI 账号昵称，或输入 /done 结束创建：");
            string nicknameInput = ReadRequiredLine();
            string nickname = nicknameInput.Trim();

            if (nickname.Equals("/done", StringComparison.OrdinalIgnoreCase))
            {
                if (_aiAccountService.GetAllAccounts().Count == 0)
                {
                    Console.WriteLine("至少需要创建一个 AI 账号后才能结束。");
                    continue;
                }

                break;
            }

            string? validationError = _aiAccountService.ValidateNickname(nickname);

            if (validationError is not null)
            {
                Console.WriteLine(validationError);
                continue;
            }

            Console.Write("请输入身份描述（可留空）：");
            string identityDescription = ReadRequiredLine();

            Console.Write("请输入 VC号（可留空，系统将生成 7 位随机数字号）：");
            string vcNumber = ReadRequiredLine();

            Console.Write("请输入性格（可留空）：");
            string personality = ReadRequiredLine();

            Console.Write("请输入说话风格（可留空）：");
            string speakingStyle = ReadRequiredLine();

            bool creationSucceeded = _aiAccountService.TryCreateAiAccount(
                nickname,
                vcNumber,
                identityDescription,
                personality,
                speakingStyle,
                out AiAccount? aiAccount,
                out string creationError);

            if (!creationSucceeded || aiAccount is null)
            {
                Console.WriteLine(creationError);
                continue;
            }

            Console.WriteLine(
                $"已创建 AI 账号：{aiAccount.Nickname}（VC号：{aiAccount.VcNumber}）");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 按创建顺序显示当前数据库中保存的全部 AI 账号。
    /// </summary>
    private static void DisplayAiAccounts(IReadOnlyList<AiAccount> aiAccounts)
    {
        Console.WriteLine();
        Console.WriteLine("已创建的 AI 账号：");

        for (int i = 0; i < aiAccounts.Count; i++)
        {
            AiAccount account = aiAccounts[i];

            Console.WriteLine($"{i + 1}. {account.Nickname}");
            Console.WriteLine($"   VC号：{account.VcNumber}");
            Console.WriteLine($"   身份描述：{DisplayText(account.IdentityDescription)}");
            Console.WriteLine($"   性格：{DisplayText(account.Personality)}");
            Console.WriteLine($"   说话风格：{DisplayText(account.SpeakingStyle)}");
            Console.WriteLine($"   创建时间：{account.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }
    }

    /// <summary>
    /// 查询数据库中的已有群聊；用户可以按编号进入，也可以输入 /new 创建新群聊。
    /// </summary>
    private GroupChat SelectOrCreateGroupChat(IReadOnlyList<AiAccount> aiAccounts)
    {
        IReadOnlyList<GroupChat> existingGroupChats =
            _groupChatService.GetAllGroupChats();

        if (existingGroupChats.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("当前没有已有群聊，将创建一个新群聊。");
            return CreateGroupChat(aiAccounts);
        }

        while (true)
        {
            DisplayGroupChatOptions(existingGroupChats);
            Console.Write("请输入群聊编号进入，或输入 /new 创建新群聊：");
            string input = ReadRequiredLine().Trim();

            if (input.Equals("/new", StringComparison.OrdinalIgnoreCase))
            {
                return CreateGroupChat(aiAccounts);
            }

            if (!int.TryParse(input, out int groupChatNumber))
            {
                Console.WriteLine("请输入有效的群聊编号或 /new。");
                continue;
            }

            if (groupChatNumber < 1 || groupChatNumber > existingGroupChats.Count)
            {
                Console.WriteLine($"群聊编号 {groupChatNumber} 不存在，请重新输入。");
                continue;
            }

            return existingGroupChats[groupChatNumber - 1];
        }
    }

    /// <summary>
    /// 按数据库查询顺序显示已有群聊及其成员数量。
    /// </summary>
    private static void DisplayGroupChatOptions(
        IReadOnlyList<GroupChat> groupChats)
    {
        Console.WriteLine();
        Console.WriteLine("已有群聊：");

        for (int i = 0; i < groupChats.Count; i++)
        {
            GroupChat groupChat = groupChats[i];
            Console.WriteLine(
                $"{i + 1}. {groupChat.Name} "
                + $"（{groupChat.Members.Count} 个 AI 成员，"
                + $"创建于 {groupChat.CreatedAt:yyyy-MM-dd HH:mm:ss}）");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// 获取群聊名称和成员编号，并调用群聊 Service 创建群聊。
    /// </summary>
    private GroupChat CreateGroupChat(IReadOnlyList<AiAccount> aiAccounts)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("开始创建群聊。");

            string groupName = ReadGroupChatName();

            Console.WriteLine();
            Console.WriteLine("请选择要加入群聊的 AI 账号。");
            DisplayAiAccountOptions(aiAccounts);

            List<Guid> selectedAiAccountIds = SelectGroupMemberIds(aiAccounts);
            bool creationSucceeded = _groupChatService.TryCreateGroupChat(
                groupName,
                selectedAiAccountIds,
                out GroupChat? groupChat,
                out string creationError);

            if (creationSucceeded && groupChat is not null)
            {
                return groupChat;
            }

            Console.WriteLine(creationError);
        }
    }

    /// <summary>
    /// 循环读取群聊名称，并通过群聊 Service 完成名称验证。
    /// </summary>
    private string ReadGroupChatName()
    {
        while (true)
        {
            Console.Write("请输入群聊名称：");
            string groupName = ReadRequiredLine().Trim();
            string? validationError = _groupChatService.ValidateGroupChatName(groupName);

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
    private static List<Guid> SelectGroupMemberIds(IReadOnlyList<AiAccount> aiAccounts)
    {
        while (true)
        {
            Console.Write("请输入成员编号，多个编号用英文逗号分隔，例如 1,3：");
            string input = ReadRequiredLine();

            if (TryGetSelectedMemberIndexes(input, aiAccounts.Count, out List<int> selectedIndexes))
            {
                return selectedIndexes
                    .Select(index => aiAccounts[index].Id)
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
    /// 显示当前选择或创建的群聊信息和 AI 账号成员列表。
    /// </summary>
    private void DisplayGroupChat(GroupChat groupChat)
    {
        Console.WriteLine();
        Console.WriteLine("群聊信息：");
        Console.WriteLine($"群聊名称：{groupChat.Name}");
        Console.WriteLine($"创建时间：{groupChat.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("群成员：");

        IReadOnlyList<AiAccount> members = _groupChatService.GetMembers(groupChat);

        for (int i = 0; i < members.Count; i++)
        {
            AiAccount member = members[i];
            Console.WriteLine($"{i + 1}. {member.Nickname}");
        }
    }

    /// <summary>
    /// 处理聊天输入，并调用共享交互 Service 保存用户消息和模拟 AI 回复。
    /// </summary>
    private async Task EnterGroupChatAsync(GroupChat groupChat)
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

            GroupChatInteractionResult result = await _interactionService
                .ProcessUserMessageAsync(groupChat, content);

            if (result.Status == GroupChatInteractionStatus.UserMessageRejected)
            {
                Console.WriteLine(result.ErrorMessage);
                continue;
            }

            if (result.UserMessage is not null)
            {
                DisplayMessage(result.UserMessage);
            }

            if (result.SpeakerSelectionStatus
                == AiSpeakerSelectionStatus.MentionNotMatched)
            {
                Console.WriteLine("没有找到被点名的群内 AI，将按默认规则选择发言者。");
            }

            foreach (GroupMessage aiReply in result.AiReplies)
            {
                DisplayMessage(aiReply);
            }

            if (result.Status is GroupChatInteractionStatus.AiReplyFailed
                or GroupChatInteractionStatus.PartiallySucceeded)
            {
                Console.WriteLine(result.ErrorMessage);
                continue;
            }
        }
    }

    /// <summary>
    /// 获取并显示当前群聊按发送时间排列的完整聊天记录。
    /// </summary>
    private void DisplayChatHistory(GroupChat groupChat)
    {
        Console.WriteLine();
        Console.WriteLine($"群聊“{groupChat.Name}”的聊天记录：");

        IReadOnlyList<GroupMessage> orderedMessages =
            _groupMessageService.GetOrderedChatHistory(groupChat);

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

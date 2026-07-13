using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Services;

/// <summary>
/// 负责根据 AI 账号资料和用户消息生成本地模拟回复。
/// </summary>
public class FakeAiReplyService
{
    /// <summary>
    /// 生成不依赖网络或真实大模型的假 AI 回复文本。
    /// </summary>
    public string GenerateReply(AiAccount aiSpeaker, string userContent)
    {
        string identityDescription = string.IsNullOrWhiteSpace(aiSpeaker.IdentityDescription)
            ? "暂未填写身份描述"
            : aiSpeaker.IdentityDescription;
        string personality = string.IsNullOrWhiteSpace(aiSpeaker.Personality)
            ? "暂未填写性格"
            : aiSpeaker.Personality;
        string speakingStyle = string.IsNullOrWhiteSpace(aiSpeaker.SpeakingStyle)
            ? "暂未填写说话风格"
            : aiSpeaker.SpeakingStyle;

        return $"我是{aiSpeaker.Nickname}，{identityDescription}，性格是{personality}，"
            + $"说话风格是{speakingStyle}。我看到了你刚才说的“{userContent}”。"
            + "这是当前由系统生成的模拟回复。";
    }
}

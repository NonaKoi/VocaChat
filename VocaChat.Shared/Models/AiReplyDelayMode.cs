namespace VocaChat.Models;

/// <summary>
/// 表示 AI 消息采用固定回复间隔，还是在用户指定区间内随机选择间隔。
/// </summary>
public enum AiReplyDelayMode
{
    Fixed,
    RandomRange
}

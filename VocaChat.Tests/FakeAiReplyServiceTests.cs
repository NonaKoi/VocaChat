using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证模拟回复文本仍然包含当前发言者和本轮消息上下文。
/// </summary>
public sealed class FakeAiReplyServiceTests
{
    [Fact]
    public void GenerateReply_ReturnsNonBlankLocalReplyWithSpeakerAndUserContent()
    {
        AiAccount aiSpeaker = new(
            "Alpha#01",
            "Alpha",
            "助手",
            "冷静",
            "简洁");
        FakeAiReplyService service = new();

        string reply = service.GenerateReply(aiSpeaker, "hello");

        Assert.False(string.IsNullOrWhiteSpace(reply));
        Assert.Contains("Alpha", reply);
        Assert.Contains("hello", reply);
        Assert.Contains("模拟回复", reply);
    }

    [Fact]
    public void GenerateFollowUpReply_ReferencesPrimarySpeakerAndUserContent()
    {
        AiAccount primarySpeaker = new("Alpha#01", "Alpha", "", "", "");
        AiAccount followUpSpeaker = new("Beta#01", "Beta", "", "", "温和");
        FakeAiReplyService service = new();

        string reply = service.GenerateFollowUpReply(
            followUpSpeaker,
            primarySpeaker,
            "一起讨论周末安排");

        Assert.Contains("Beta", reply);
        Assert.Contains("Alpha", reply);
        Assert.Contains("一起讨论周末安排", reply);
        Assert.Contains("模拟跟进回复", reply);
    }
}

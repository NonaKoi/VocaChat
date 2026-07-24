using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证信息增量判断只比较同一发言者，并识别换词复述与真实立场变化。
/// </summary>
public sealed class ConversationInformationGainEvaluatorTests
{
    [Fact]
    public void IsNearDuplicate_WithParaphrasedMessage_ReturnsTrue()
    {
        Assert.True(ConversationInformationGainEvaluator.IsNearDuplicate(
            "我觉得周末去旧书店会比较合适。",
            "我觉得周末去旧书店应该更合适。"));
    }

    [Fact]
    public void IsNearDuplicate_WithChangedPolarity_ReturnsFalse()
    {
        Assert.False(ConversationInformationGainEvaluator.IsNearDuplicate(
            "我觉得周末去旧书店会比较合适。",
            "我觉得周末不去旧书店会比较合适。"));
    }

    [Fact]
    public void AssessRound_RepeatedSameSpeakerMessage_IsLowInformation()
    {
        Guid speakerId = Guid.NewGuid();

        ConversationInformationGainAssessment assessment =
            ConversationInformationGainEvaluator.AssessRound(
                new[]
                {
                    new ConversationInformationMessage(
                        speakerId,
                        "我觉得周末去旧书店应该更合适。")
                },
                new[]
                {
                    new ConversationInformationMessage(
                        speakerId,
                        "我觉得周末去旧书店会比较合适。")
                });

        Assert.True(assessment.IsLowInformation);
        Assert.True(assessment.Score < 0.38);
    }

    [Fact]
    public void AssessRound_SameContentFromOtherSpeaker_IsNotCompared()
    {
        ConversationInformationGainAssessment assessment =
            ConversationInformationGainEvaluator.AssessRound(
                new[]
                {
                    new ConversationInformationMessage(
                        Guid.NewGuid(),
                        "我觉得周末去旧书店会比较合适。")
                },
                new[]
                {
                    new ConversationInformationMessage(
                        Guid.NewGuid(),
                        "我觉得周末去旧书店会比较合适。")
                });

        Assert.False(assessment.IsLowInformation);
        Assert.Equal(1, assessment.Score);
    }
}

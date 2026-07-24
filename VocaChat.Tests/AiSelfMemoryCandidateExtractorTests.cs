using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Tests;

/// <summary>
/// 验证保存后候选提取只识别少量具有跨轮价值的本人内容。
/// </summary>
public sealed class AiSelfMemoryCandidateExtractorTests
{
    [Fact]
    public void Extract_CreatesPlanCandidateFromPersistedSpeakerMessage()
    {
        AiAccount speaker = CreateAccount();
        AiPersistedMessageEvidence message = new(
            Guid.NewGuid(),
            "我准备下个月办一场小型插画展。",
            new DateTime(2026, 7, 23, 10, 0, 0));

        IReadOnlyList<AiSelfMemoryProposal> proposals =
            new AiSelfMemoryCandidateExtractor().Extract(
                CreateRequest(speaker),
                Array.Empty<AiConversationSelfMemory>(),
                new[] { message });

        AiSelfMemoryProposal proposal = Assert.Single(proposals);
        Assert.Equal(AiSelfMemoryType.Plan, proposal.Type);
        Assert.Equal(speaker.Id, proposal.SubjectAiAccountId);
        Assert.Equal(speaker.CharacterWorldId, proposal.CharacterWorldId);
        Assert.Equal(message.Content, proposal.Summary);
        Assert.StartsWith("auto.plan.", proposal.FactKey);
    }

    [Fact]
    public void Extract_DoesNotTreatQuestionAboutOtherPersonAsOwnMemory()
    {
        AiAccount speaker = CreateAccount();

        IReadOnlyList<AiSelfMemoryProposal> proposals =
            new AiSelfMemoryCandidateExtractor().Extract(
                CreateRequest(speaker),
                Array.Empty<AiConversationSelfMemory>(),
                new[]
                {
                    new AiPersistedMessageEvidence(
                        Guid.NewGuid(),
                        "你最近正在准备什么？",
                        DateTime.Now)
                });

        Assert.Empty(proposals);
    }

    [Fact]
    public void Extract_SkipsContentAlreadyCoveredByActiveMemory()
    {
        AiAccount speaker = CreateAccount();
        AiConversationSelfMemory existing = new(
            Guid.NewGuid(),
            speaker.Id,
            AiSelfMemoryType.Preference,
            "我更喜欢安静一点的咖啡馆。",
            "preference.quiet-cafe",
            AiSelfMemoryFactNature.Subjective,
            AiSelfMemoryMutability.Evolving,
            AiSelfMemoryTrustLevel.SubjectiveState,
            speaker.CharacterWorldId,
            AiSelfMemorySource.Director,
            50,
            false,
            null,
            DateTime.Now);

        IReadOnlyList<AiSelfMemoryProposal> proposals =
            new AiSelfMemoryCandidateExtractor().Extract(
                CreateRequest(speaker),
                new[] { existing },
                new[]
                {
                    new AiPersistedMessageEvidence(
                        Guid.NewGuid(),
                        "我更喜欢安静一点的咖啡馆。",
                        DateTime.Now)
                });

        Assert.Empty(proposals);
    }

    [Fact]
    public void Extract_LimitsOneTurnToTwoCandidates()
    {
        AiAccount speaker = CreateAccount();
        DateTime sentAt = DateTime.Now;

        IReadOnlyList<AiSelfMemoryProposal> proposals =
            new AiSelfMemoryCandidateExtractor().Extract(
                CreateRequest(speaker),
                Array.Empty<AiConversationSelfMemory>(),
                new[]
                {
                    new AiPersistedMessageEvidence(
                        Guid.NewGuid(),
                        "我最近一直在整理旧照片。",
                        sentAt),
                    new AiPersistedMessageEvidence(
                        Guid.NewGuid(),
                        "我打算周末去看看新的画材。",
                        sentAt.AddSeconds(1)),
                    new AiPersistedMessageEvidence(
                        Guid.NewGuid(),
                        "我以前去过那座临海的小城。",
                        sentAt.AddSeconds(2))
                });

        Assert.Equal(2, proposals.Count);
    }

    private static AiAccount CreateAccount() => new(
        $"vc-{Guid.NewGuid():N}",
        "候选提取测试账号",
        string.Empty,
        string.Empty,
        string.Empty);

    private static AiMessageGenerationRequest CreateRequest(
        AiAccount speaker)
    {
        return new AiMessageGenerationRequest
        {
            Scenario = AiMessageGenerationScenario.UserPrivateChat,
            Speaker = speaker,
            FocusContent = "最近在忙什么",
            ExpectedMessageCount = 1
        };
    }
}

using VocaChat.Models;
using VocaChat.Services;
using VocaChat.Tests.TestSupport;

namespace VocaChat.Tests;

public sealed class AiWorldKnowledgeCandidateExtractorTests
{
    private readonly AiWorldKnowledgeCandidateExtractor _extractor = new();

    [Fact]
    public void Extract_WithGreeting_ReturnsNoSignal()
    {
        AiAccount source = CreateAccount("问候者");

        AiWorldKnowledgeExtraction result =
            _extractor.Extract(source, "你好。");

        Assert.Equal(AiWorldKnowledgeSignal.None, result.Signal);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Extract_WithWorldEntity_ReturnsGroundedUnfamiliarConcept()
    {
        AiAccount source = CreateAccount("世界介绍者");

        AiWorldKnowledgeExtraction result = _extractor.Extract(
            source,
            "阿拜多斯是一所受到沙漠化影响的高中。");

        AiWorldKnowledgeCandidate candidate =
            Assert.Single(result.Candidates);
        Assert.Equal(
            AiWorldKnowledgeSignal.UnfamiliarConcept,
            result.Signal);
        Assert.Equal(source.Id, candidate.SubjectAiAccountId);
        Assert.Contains("阿拜多斯", candidate.Summary);
        Assert.Equal(
            AiWorldKnowledgeTrustLevel.DirectStatement,
            candidate.TrustLevel);
    }

    [Fact]
    public void Extract_WithExplicitCrossWorldStatement_UsesStrongestSignal()
    {
        AiAccount source = CreateAccount("确认者");

        AiWorldKnowledgeExtraction result = _extractor.Extract(
            source,
            "我们不在同一个世界，但现在确实可以跨世界通信。");

        Assert.Equal(
            AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation,
            result.Signal);
        Assert.Equal(
            AiWorldKnowledgeSignal.ExplicitCrossWorldConfirmation,
            Assert.Single(result.Candidates).Signal);
    }

    [Fact]
    public void Extract_WithSynonymousSchoolPhrases_UsesSameTopicKey()
    {
        AiAccount source = CreateAccount("同义知识讲述者");

        AiWorldKnowledgeCandidate first = Assert.Single(
            _extractor.Extract(
                    source,
                    "阿拜多斯是一所位于沙漠边缘的高中。")
                .Candidates);
        AiWorldKnowledgeCandidate second = Assert.Single(
            _extractor.Extract(
                    source,
                    "阿拜多斯是个学校，现在仍受到沙漠化影响。")
                .Candidates);

        Assert.Equal(first.KnowledgeKey, second.KnowledgeKey);
    }

    [Fact]
    public async Task ExtractAsync_DeterministicMatch_DoesNotCallSemanticModel()
    {
        StaticAiWorldKnowledgeSemanticExtractor semanticExtractor = new();
        AiWorldKnowledgeCandidateExtractor extractor =
            new(semanticExtractor);
        AiAccount source = CreateAccount("规则优先测试者");

        AiWorldKnowledgeExtraction result = await extractor.ExtractAsync(
            source,
            "阿拜多斯是一所高中。",
            allowSemanticFallback: true);

        Assert.Equal(
            AiWorldKnowledgeSignal.UnfamiliarConcept,
            result.Signal);
        Assert.Empty(semanticExtractor.Requests);
    }

    [Fact]
    public async Task ExtractAsync_SemanticMatch_UsesExactConceptForStableKey()
    {
        StaticAiWorldKnowledgeSemanticExtractor semanticExtractor = new(
            _ => new AiWorldKnowledgeSemanticExtractionResult(
                AiWorldKnowledgeSignal.UnfamiliarConcept,
                new[]
                {
                    new AiWorldKnowledgeSemanticConcept(
                        "潮汐门",
                        AiWorldKnowledgeConceptCategory.Place)
                },
                ErrorMessage: null));
        AiWorldKnowledgeCandidateExtractor extractor =
            new(semanticExtractor);
        AiAccount source = CreateAccount("语义提取测试者");

        AiWorldKnowledgeExtraction result = await extractor.ExtractAsync(
            source,
            "潮汐门的钟声只在蓝月落下时响起。",
            allowSemanticFallback: true);

        Assert.True(result.UsedSemanticExtractor);
        Assert.Equal(
            AiWorldKnowledgeSignal.UnfamiliarConcept,
            result.Signal);
        Assert.Single(result.Candidates);
        Assert.Single(semanticExtractor.Requests);
    }

    private static AiAccount CreateAccount(string nickname)
    {
        return new AiAccount(
            Guid.NewGuid().ToString("N")[..7],
            nickname,
            string.Empty,
            string.Empty,
            string.Empty);
    }
}

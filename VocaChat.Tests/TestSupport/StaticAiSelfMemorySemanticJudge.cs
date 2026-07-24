using VocaChat.Services;

namespace VocaChat.Tests.TestSupport;

internal sealed class StaticAiSelfMemorySemanticJudge
    : IAiSelfMemorySemanticJudge
{
    private readonly Func<
        AiSelfMemorySemanticJudgmentRequest,
        AiSelfMemorySemanticJudgmentResult> _createResult;

    public StaticAiSelfMemorySemanticJudge(
        Func<
            AiSelfMemorySemanticJudgmentRequest,
            AiSelfMemorySemanticJudgmentResult>? createResult = null)
    {
        _createResult = createResult ?? AcceptAll;
    }

    public List<AiSelfMemorySemanticJudgmentRequest> Requests { get; } = new();

    public Task<AiSelfMemorySemanticJudgmentResult> JudgeAsync(
        AiSelfMemorySemanticJudgmentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(_createResult(request));
    }

    private static AiSelfMemorySemanticJudgmentResult AcceptAll(
        AiSelfMemorySemanticJudgmentRequest request)
    {
        IReadOnlyList<AiSelfMemorySemanticDecision> decisions = request
            .Proposals
            .Select((proposal, index) => new AiSelfMemorySemanticDecision(
                index,
                proposal.Operation switch
                {
                    AiSelfMemoryProposalOperation.Update
                        => AiSelfMemorySemanticOutcome.Supersede,
                    AiSelfMemoryProposalOperation.Archive
                        => AiSelfMemorySemanticOutcome.Archive,
                    _ => AiSelfMemorySemanticOutcome.Accept
                },
                proposal.TargetMemoryId,
                proposal.FactKey,
                proposal.FactNature,
                proposal.Mutability,
                "测试语义判断通过。"))
            .ToList()
            .AsReadOnly();

        return new AiSelfMemorySemanticJudgmentResult(
            decisions,
            false,
            string.Empty);
    }
}

using VocaChat.Services;

namespace VocaChat.Tests.TestSupport;

internal sealed class StaticAiWorldKnowledgeSemanticExtractor
    : IAiWorldKnowledgeSemanticExtractor
{
    private readonly Func<
        AiWorldKnowledgeSemanticExtractionRequest,
        AiWorldKnowledgeSemanticExtractionResult> _createResult;

    public StaticAiWorldKnowledgeSemanticExtractor(
        Func<
            AiWorldKnowledgeSemanticExtractionRequest,
            AiWorldKnowledgeSemanticExtractionResult>? createResult = null)
    {
        _createResult = createResult
            ?? (_ => AiWorldKnowledgeSemanticExtractionResult.None);
    }

    public List<AiWorldKnowledgeSemanticExtractionRequest> Requests
    {
        get;
    } = new();

    public Task<AiWorldKnowledgeSemanticExtractionResult> ExtractAsync(
        AiWorldKnowledgeSemanticExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(_createResult(request));
    }
}

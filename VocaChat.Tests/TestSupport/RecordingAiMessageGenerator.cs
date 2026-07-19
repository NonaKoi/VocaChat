using VocaChat.Services;

namespace VocaChat.Tests.TestSupport;

/// <summary>
/// 记录业务层实际提交的生成请求，并返回可预测的短消息。
/// </summary>
internal sealed class RecordingAiMessageGenerator : IAiMessageGenerator
{
    public List<AiMessageGenerationRequest> Requests { get; } = new();

    public Task<IReadOnlyList<string>> GenerateMessagesAsync(
        AiMessageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        int requestNumber = Requests.Count;
        IReadOnlyList<string> messages = Enumerable
            .Range(1, request.ExpectedMessageCount)
            .Select(index => $"{request.Speaker.Nickname}-{requestNumber}-{index}")
            .ToList()
            .AsReadOnly();
        return Task.FromResult(messages);
    }
}

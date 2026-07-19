namespace VocaChat.Services;

/// <summary>
/// 在模型分析不可用或测试不需要网络时提供安全的中性洞察。
/// </summary>
public sealed class RuleBasedSessionInsightAnalyzer : ISessionInsightAnalyzer
{
    private const string FallbackReason =
        "未使用语义分析，采用基础关系变化且不提取长期记忆。";

    public Task<SessionInsightAnalysis> AnalyzeAsync(
        SessionInsightAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SessionInsightAnalysis.Fallback(FallbackReason));
    }
}

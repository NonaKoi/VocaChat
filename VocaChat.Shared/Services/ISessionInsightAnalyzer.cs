namespace VocaChat.Services;

/// <summary>
/// 在 Session 结束后生成结构化洞察，不负责修改关系或保存记忆。
/// </summary>
public interface ISessionInsightAnalyzer
{
    Task<SessionInsightAnalysis> AnalyzeAsync(
        SessionInsightAnalysisRequest request,
        CancellationToken cancellationToken = default);
}

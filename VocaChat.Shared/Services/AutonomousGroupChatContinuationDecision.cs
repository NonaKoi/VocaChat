namespace VocaChat.Services;

/// <summary>
/// 返回下一轮概率、随机值及本次是否继续。
/// </summary>
public sealed class AutonomousGroupChatContinuationDecision
{
    public double RetentionFactor { get; init; }
    public double OccurrenceProbability { get; init; }
    public double RandomRoll { get; init; }
    public bool ShouldContinue { get; init; }
}

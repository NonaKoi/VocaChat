namespace VocaChat.Services;

/// <summary>
/// 表示一次下一轮概率计算和随机判断结果。
/// </summary>
public sealed class AutonomousPrivateChatContinuationDecision
{
    public double RetentionFactor { get; init; }
    public double OccurrenceProbability { get; init; }
    public double RandomRoll { get; init; }
    public bool ShouldContinue { get; init; }
}

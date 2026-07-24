namespace VocaChat.Models;

/// <summary>
/// 表示一个 AI 账号对另一个 AI 账号所处世界差异的方向性认知阶段。
/// </summary>
public enum AiWorldAwarenessState
{
    AssumedSharedWorld,
    AnomalyObserved,
    DifferentBackgroundRecognized,
    CrossWorldConfirmed
}

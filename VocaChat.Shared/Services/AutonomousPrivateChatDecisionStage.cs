namespace VocaChat.Services;

/// <summary>
/// 表示自主私信判断停止或通过的明确阶段。
/// </summary>
public enum AutonomousPrivateChatDecisionStage
{
    Approved,
    SelfInteractionNotAllowed,
    AccountNotFound,
    GlobalDisabled,
    PrivateChatsDisabled,
    ParticipantDisabled,
    NoEligibleInitiator,
    CooldownActive,
    ScoreBelowThreshold
}

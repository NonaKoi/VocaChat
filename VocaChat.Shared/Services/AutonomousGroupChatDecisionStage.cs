namespace VocaChat.Services;

/// <summary>
/// 表示一次自主好友群聊判断停止或通过的阶段。
/// </summary>
public enum AutonomousGroupChatDecisionStage
{
    Approved,
    TooFewParticipants,
    TooManyParticipants,
    DuplicateParticipant,
    AccountNotFound,
    GlobalDisabled,
    GroupChatsDisabled,
    ParticipantDisabled,
    ParticipantCannotJoin,
    NoEligibleInitiator,
    ScoreBelowThreshold
}

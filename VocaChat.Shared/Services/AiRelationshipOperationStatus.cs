namespace VocaChat.Services;

/// <summary>
/// 表示好友关系读取或保存操作的明确业务结果。
/// </summary>
public enum AiRelationshipOperationStatus
{
    Success,
    SelfRelationshipNotAllowed,
    AccountNotFound,
    ValueOutOfRange
}

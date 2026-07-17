namespace VocaChat.Models;

/// <summary>
/// 区分本地用户参与的私信与仅由两个 AI 账号参与的好友私信。
/// </summary>
public enum PrivateChatKind
{
    LocalUserAndAiAccount,
    AiAccounts
}

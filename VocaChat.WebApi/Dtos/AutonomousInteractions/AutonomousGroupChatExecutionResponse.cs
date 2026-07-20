using VocaChat.WebApi.Dtos.GroupChats;
using VocaChat.WebApi.Dtos.GroupMessages;

namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>
/// 返回一次自主好友群聊执行结果及已经保存的正式消息。
/// </summary>
public sealed class AutonomousGroupChatExecutionResponse
{
    public string Status { get; init; } = string.Empty;
    public AutonomousGroupChatDecisionResponse Decision { get; init; } = null!;
    public GroupChatResponse? GroupChat { get; init; }
    public bool GroupChatCreated { get; init; }
    public AutonomousGroupChatSessionResponse? Session { get; init; }
    public IReadOnlyList<AutonomousGroupChatRoundResponse> Rounds { get; init; }
        = Array.Empty<AutonomousGroupChatRoundResponse>();
    public IReadOnlyList<GroupMessageResponse> Messages { get; init; } =
        Array.Empty<GroupMessageResponse>();
    public string? ErrorMessage { get; init; }
}

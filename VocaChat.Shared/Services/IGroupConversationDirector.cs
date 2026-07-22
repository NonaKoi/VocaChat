namespace VocaChat.Services;

/// <summary>
/// 在用户群聊中制定整轮发言者、回复目标和语义分工，不生成可见台词。
/// </summary>
public interface IGroupConversationDirector
{
    Task<GroupConversationTurnPlan> CreatePlanAsync(
        GroupConversationPlanningRequest request,
        CancellationToken cancellationToken = default);
}

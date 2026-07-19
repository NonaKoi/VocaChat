namespace VocaChat.Services;

/// <summary>
/// 在业务规则确定发言者、轮次和目标消息之后，为单次生成制定语义计划。
/// </summary>
public interface IConversationDirector
{
    Task<ConversationDirectionPlan> CreatePlanAsync(
        AiMessageGenerationRequest request,
        CancellationToken cancellationToken = default);
}

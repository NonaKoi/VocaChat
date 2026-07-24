namespace VocaChat.Models;

/// <summary>
/// 区分一次模型调用在聊天生成链路中承担的职责。
/// </summary>
public enum AiModelInvocationStage
{
    GroupDirector,
    ConversationDirector,
    ReplyGeneration,
    SelfMemoryJudgment,
    SessionInsight,
    WorldKnowledgeExtraction
}

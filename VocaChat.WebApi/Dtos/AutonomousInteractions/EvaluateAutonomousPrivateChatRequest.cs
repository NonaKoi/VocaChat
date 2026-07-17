namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>表示一次自主私信判断预览的两个候选好友。</summary>
public sealed class EvaluateAutonomousPrivateChatRequest
{
    public Guid FirstAiAccountId { get; set; }
    public Guid SecondAiAccountId { get; set; }
}

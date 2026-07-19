namespace VocaChat.WebApi.Dtos.AutonomousInteractions;

/// <summary>指定本次受控自主私信判断使用的两位好友。</summary>
public sealed class RunAutonomousPrivateChatRequest
{
    public Guid FirstAiAccountId { get; init; }
    public Guid SecondAiAccountId { get; init; }
    public string? Topic { get; init; }
}

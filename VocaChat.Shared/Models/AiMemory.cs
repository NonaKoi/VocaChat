namespace VocaChat.Models;

/// <summary>
/// 表示一个 AI 账号对另一个 AI 账号持有的有方向长期记忆。
/// </summary>
public class AiMemory
{
    internal const int SummaryMaxLength = 500;
    internal const int MinimumSalience = 1;
    internal const int MaximumSalience = 100;

    public Guid Id { get; private set; }
    public Guid OwnerAiAccountId { get; private set; }
    public Guid SubjectAiAccountId { get; private set; }
    public AiMemoryType Type { get; private set; }
    public string Summary { get; private set; }
    public int Salience { get; private set; }
    public Guid SourcePrivateChatId { get; private set; }
    public Guid SourceSessionId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime? LastRecalledAt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public AiAccount OwnerAiAccount { get; private set; }
    public AiAccount SubjectAiAccount { get; private set; }
    public PrivateChat SourcePrivateChat { get; private set; }
    public AutonomousPrivateChatSession SourceSession { get; private set; }

    private AiMemory()
    {
        Summary = string.Empty;
        OwnerAiAccount = null!;
        SubjectAiAccount = null!;
        SourcePrivateChat = null!;
        SourceSession = null!;
    }

    internal AiMemory(
        Guid ownerAiAccountId,
        Guid subjectAiAccountId,
        AiMemoryType type,
        string summary,
        int salience,
        Guid sourcePrivateChatId,
        Guid sourceSessionId,
        DateTime occurredAt,
        DateTime createdAt)
    {
        Id = Guid.NewGuid();
        OwnerAiAccountId = ownerAiAccountId;
        SubjectAiAccountId = subjectAiAccountId;
        Type = type;
        Summary = summary;
        Salience = salience;
        SourcePrivateChatId = sourcePrivateChatId;
        SourceSessionId = sourceSessionId;
        OccurredAt = occurredAt;
        IsActive = true;
        CreatedAt = createdAt;
        OwnerAiAccount = null!;
        SubjectAiAccount = null!;
        SourcePrivateChat = null!;
        SourceSession = null!;
    }
}

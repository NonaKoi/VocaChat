namespace VocaChat.Models;

/// <summary>
/// 表示一个可由多个 AI 账号共享的角色世界设定。
/// </summary>
public sealed class CharacterWorld
{
    internal const int NameMaxLength = 100;
    internal const int DescriptionMaxLength = 4000;

    public static readonly Guid DefaultWorldId =
        new("2d215860-2e59-4b55-916c-fc5cb6e96c27");

    public const string DefaultWorldName = "现实世界";
    public const string DefaultWorldDescription =
        "采用现代现实社会的基本规则；未经用户或可靠来源确认的时效性外部信息不得作为确定事实。";

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// 供 EF Core 从数据库还原实体使用。
    /// </summary>
    private CharacterWorld()
    {
        Name = string.Empty;
        Description = string.Empty;
    }

    /// <summary>
    /// 创建一个由用户定义的新角色世界。
    /// </summary>
    internal CharacterWorld(string name, string description)
    {
        DateTime now = DateTime.UtcNow;

        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// 更新世界名称和权威说明，不改变已有账号与该世界的关联。
    /// </summary>
    internal void Update(string name, string description)
    {
        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }
}

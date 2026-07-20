namespace VocaChat.Services;

/// <summary>
/// 集中提供自主好友群聊执行使用的有界随机值，便于测试替换固定序列。
/// </summary>
public class AutonomousGroupChatRandomSource
{
    private readonly Random _random;

    public AutonomousGroupChatRandomSource()
        : this(Random.Shared)
    {
    }

    internal AutonomousGroupChatRandomSource(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public virtual double NextUnit()
    {
        return _random.NextDouble();
    }
}

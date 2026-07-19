namespace VocaChat.Services;

/// <summary>
/// 集中提供自主私信执行使用的有界随机值；测试可传入固定 Random 实例。
/// </summary>
public sealed class AutonomousPrivateChatRandomSource
{
    private readonly Random _random;

    public AutonomousPrivateChatRandomSource()
        : this(Random.Shared)
    {
    }

    internal AutonomousPrivateChatRandomSource(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public double NextUnit()
    {
        return _random.NextDouble();
    }

    public double NextJudgeJitter()
    {
        return NextUnit() * 20 - 10;
    }
}

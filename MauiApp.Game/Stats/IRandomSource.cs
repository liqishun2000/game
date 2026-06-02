namespace MauiApp.Game.Stats;

/// <summary>
/// 可注入的随机源，便于战斗/大地图结算的可复现与单元测试。
/// </summary>
public interface IRandomSource
{
    int Next(int maxExclusive);
    double NextDouble();
}

/// <summary>
/// 基于种子的确定性随机源（默认实现）。
/// </summary>
public sealed class DeterministicRandom : IRandomSource
{
    private readonly Random _random;

    public DeterministicRandom(int seed) => _random = new Random(seed);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    public double NextDouble() => _random.NextDouble();
}

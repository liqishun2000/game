using MauiApp.Game.Stats;

namespace MauiApp.Game.Tests;

/// <summary>可控随机源：NextDouble 依次返回给定值，用尽后返回 0。</summary>
internal sealed class FakeRandom : IRandomSource
{
    private readonly Queue<double> _doubles;

    public FakeRandom(params double[] doubles) => _doubles = new Queue<double>(doubles);

    public int Next(int maxExclusive) => 0;

    public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : 0.0;
}

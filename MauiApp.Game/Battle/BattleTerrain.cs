namespace MauiApp.Game.Battle;

/// <summary>战场地形类型。</summary>
public enum BattleTerrain
{
    Plain,
    Forest,
    Water,
    Mountain,
    Road,
    Fort,
}

/// <summary>战场尺寸与地形配置（可由地图 JSON 指定）。</summary>
public sealed class BattleConfig
{
    public int Width { get; init; } = 50;
    public int Height { get; init; } = 50;
    public string TerrainMode { get; init; } = "procedural_v1";

    public static BattleConfig Default50 => new();
}

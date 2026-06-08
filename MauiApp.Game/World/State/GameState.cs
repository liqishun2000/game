using MauiApp.Game.Content;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;

namespace MauiApp.Game.World.State;

/// <summary>大地图运行态总状态（可序列化为存档的核心）。</summary>
public sealed class GameState
{
    public required ContentDatabase Content { get; init; }
    public required BalanceConfig Balance { get; init; }

    public int Seed { get; set; }

    /// <summary>当前月（从 1 开始）。</summary>
    public int Month { get; set; } = 1;

    public string MapId { get; set; } = "";

    public AiDifficulty Difficulty { get; set; } = AiDifficulty.Normal;

    /// <summary>小兵实例 id 自增序列。</summary>
    public int NextUnitId { get; set; } = 1;

    public Dictionary<string, FactionState> Factions { get; } = new();
    public Dictionary<string, TileState> Tiles { get; } = new();

    public IEnumerable<TileState> TilesOf(string factionId) =>
        Tiles.Values.Where(t => t.OwnerFactionId == factionId);

    /// <summary>统计某势力某兵种的现役数量（用于特殊兵种上限）。</summary>
    public int CountUnits(string factionId, string unitTemplateId) =>
        Tiles.Values
            .Where(t => t.OwnerFactionId == factionId)
            .SelectMany(t => t.Units)
            .Count(u => u.TemplateId == unitTemplateId);

    /// <summary>某势力当前是否还存活（仍拥有地盘）。</summary>
    public bool IsAlive(string factionId) => TilesOf(factionId).Any();
}

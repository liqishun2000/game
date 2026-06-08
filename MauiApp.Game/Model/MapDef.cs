using MauiApp.Game.Battle;

namespace MauiApp.Game.Model;

/// <summary>地盘上已建成的建筑。</summary>
public sealed class PlacedBuilding
{
    public string TemplateId { get; set; } = "";
    public int Level { get; set; } = 1;
}

/// <summary>驻军里的一支小兵堆叠（同兵种 × 数量）。</summary>
public sealed class UnitStack
{
    public string TemplateId { get; set; } = "";
    public int Count { get; set; } = 1;
}

/// <summary>地盘初始驻军。</summary>
public sealed class GarrisonDef
{
    public List<string> GeneralIds { get; set; } = new();
    public List<UnitStack> Units { get; set; } = new();
}

/// <summary>地图节点（地盘）定义（02-data-model.md 4.6）。</summary>
public sealed class TileNodeDef
{
    public string Id { get; set; } = "";
    public TileType Type { get; set; } = TileType.Village;
    public string Name { get; set; } = "";
    public int Col { get; set; }
    public int Row { get; set; }
    public string OwnerFactionId { get; set; } = "";
    public bool IsRebelFixed { get; set; }
    public List<PlacedBuilding> Buildings { get; set; } = new();
    public GarrisonDef Garrison { get; set; } = new();
}

/// <summary>地图定义（含道路图，02-data-model.md 4.7）。</summary>
public sealed class MapDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<int> ColumnLayout { get; set; } = new();
    public List<TileNodeDef> Nodes { get; set; } = new();

    /// <summary>无向道路：每条为两端节点 id 组成的二元数组。</summary>
    public List<string[]> Roads { get; set; } = new();

    /// <summary>出生点：势力 id -> 节点 id。</summary>
    public Dictionary<string, string> Spawns { get; set; } = new();

    /// <summary>战场配置（地图 JSON 可覆盖）。</summary>
    public BattleConfig? BattleConfig { get; set; }
}

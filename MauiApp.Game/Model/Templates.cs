namespace MauiApp.Game.Model;

/// <summary>武将模板（只读定义，对应 02-data-model.md 4.2）。</summary>
public sealed class GeneralTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public MapStats MapStats { get; set; } = new();
    public List<string> Traits { get; set; } = new();
    public string? DefaultEquipmentId { get; set; }
    public MapStats? Growth { get; set; }
}

/// <summary>小兵兵种模板（02-data-model.md 4.3）。</summary>
public sealed class UnitTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsSpecial { get; set; }

    /// <summary>特殊兵种全局数量上限；null = 无上限（普通兵）。</summary>
    public int? MaxCount { get; set; }

    public BattleStats BattleStatsBase { get; set; } = new();
    public int Move { get; set; }
    public int FoodUpkeep { get; set; }
    public Cost RecruitCost { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    public List<string> Traits { get; set; } = new();
    public List<string> EquipSlots { get; set; } = new();
}

/// <summary>装备模板（02-data-model.md 4.4）。</summary>
public sealed class EquipmentTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsUnique { get; set; }
    public bool ForGeneralOnly { get; set; }
    public string? RequiredTechId { get; set; }
    public BattleStats StatMods { get; set; } = new();
    public List<string> Effects { get; set; } = new();
    public bool Droppable { get; set; }
}

/// <summary>建筑的单项功能。</summary>
public sealed class BuildingFunction
{
    /// <summary>produce / recruit / research / defense / capacity。</summary>
    public string Type { get; set; } = "";

    /// <summary>当 type=produce/capacity 时的资源：gold/food/techPoints。</summary>
    public string? Resource { get; set; }

    public int AmountPerTurn { get; set; }
}

/// <summary>建筑模板（02-data-model.md 4.5）。</summary>
public sealed class BuildingTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Cost Cost { get; set; } = new();
    public int BuildTurns { get; set; } = 1;
    public List<BuildingFunction> Functions { get; set; } = new();
    public int MaxPerTile { get; set; } = 1;
}

/// <summary>科技解锁内容。</summary>
public sealed class TechUnlocks
{
    public List<string> EquipmentIds { get; set; } = new();
    public List<string> UnitIds { get; set; } = new();
    public List<string> BuildingIds { get; set; } = new();
}

/// <summary>科技模板（02-data-model.md 4.8）。</summary>
public sealed class TechTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Cost Cost { get; set; } = new();
    public List<string> PrereqIds { get; set; } = new();
    public TechUnlocks Unlocks { get; set; } = new();
}

/// <summary>势力定义（初始名册与资源；地盘归属在地图里定义）。</summary>
public sealed class FactionDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#888888";
    public FactionKind Kind { get; set; }
    public AiDifficulty AiDifficulty { get; set; } = AiDifficulty.Normal;
    public Cost StartResources { get; set; } = new();
    public List<string> GeneralIds { get; set; } = new();
    public List<string> RecruitableUnitIds { get; set; } = new();
}

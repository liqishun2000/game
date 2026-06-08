using MauiApp.Game.Model;

namespace MauiApp.Game.Save;

/// <summary>存档 DTO：仅保存可变状态，模板引用在读取时由内容库重新绑定。</summary>
public sealed class SaveData
{
    public int Version { get; set; } = 2;
    public int Seed { get; set; }
    public string MapId { get; set; } = "";
    public AiDifficulty Difficulty { get; set; } = AiDifficulty.Normal;
    public int Month { get; set; }
    public int NextUnitId { get; set; }
    public List<SaveFaction> Factions { get; set; } = new();
    public List<SaveTile> Tiles { get; set; } = new();
}

public sealed class SaveFaction
{
    public string Id { get; set; } = "";
    public int Gold { get; set; }
    public int Food { get; set; }
    public int TechPoints { get; set; }
    public List<string> Researched { get; set; } = new();
    public List<string> Armory { get; set; } = new();
    public List<SaveGeneral> Prison { get; set; } = new();
}

public sealed class SaveTile
{
    public string Id { get; set; } = "";
    public string OwnerFactionId { get; set; } = "";
    public bool IsRebelFixed { get; set; }
    public List<SaveBuilding> Buildings { get; set; } = new();
    public List<SaveGeneral> Generals { get; set; } = new();
    public List<SaveUnit> Units { get; set; } = new();
}

public sealed class SaveBuilding
{
    public string TemplateId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int RemainingTurns { get; set; }
}

public sealed class SaveGeneral
{
    public string TemplateId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public string? EquipmentId { get; set; }
    public GeneralStatus Status { get; set; }
    public string? TileId { get; set; }
    public int DetainedMonths { get; set; }
    public bool ActedThisMonth { get; set; }
}

public sealed class SaveUnit
{
    public int Id { get; set; }
    public string TemplateId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string? OwnerGeneralId { get; set; }
    public string? EquipmentId { get; set; }
    public int CurHp { get; set; }
    public int Morale { get; set; }
    public string TileId { get; set; } = "";
}

using MauiApp.Game.Model;

namespace MauiApp.Game.World.State;

/// <summary>局内独立小兵单位实例（02-data-model.md 4.3）。</summary>
public sealed class UnitInstance
{
    public int Id { get; set; }
    public string TemplateId { get; set; } = "";
    public UnitTemplate Template { get; set; } = null!;
    public string FactionId { get; set; } = "";

    /// <summary>所属武将（出征时挂载）；驻军可无主。</summary>
    public string? OwnerGeneralId { get; set; }

    public string? EquipmentId { get; set; }

    public int CurHp { get; set; }
    public int Morale { get; set; } = 100;

    /// <summary>所在地盘 id。</summary>
    public string TileId { get; set; } = "";
}

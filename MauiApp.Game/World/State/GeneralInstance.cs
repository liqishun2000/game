using MauiApp.Game.Model;

namespace MauiApp.Game.World.State;

/// <summary>局内武将实例（02-data-model.md 4.2）。</summary>
public sealed class GeneralInstance
{
    public string TemplateId { get; set; } = "";
    public GeneralTemplate Template { get; set; } = null!;
    public string FactionId { get; set; } = "";

    public int Level { get; set; } = 1;
    public int Exp { get; set; }

    public string? EquipmentId { get; set; }
    public GeneralStatus Status { get; set; } = GeneralStatus.Active;

    /// <summary>所在地盘 id（被俘时为关押方监狱，TileId 置空）。</summary>
    public string? TileId { get; set; }

    /// <summary>被俘后已关押月数（用于招降概率，05 第 9 节）。</summary>
    public int DetainedMonths { get; set; }
}

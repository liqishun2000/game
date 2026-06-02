using MauiApp.Game.Model;

namespace MauiApp.Game.World.State;

/// <summary>局内地盘运行态。</summary>
public sealed class TileState
{
    public string Id { get; set; } = "";
    public TileType Type { get; set; } = TileType.Village;
    public string Name { get; set; } = "";
    public int Col { get; set; }
    public int Row { get; set; }

    public string OwnerFactionId { get; set; } = "";
    public bool IsRebelFixed { get; set; }

    public List<PlacedBuildingState> Buildings { get; } = new();

    /// <summary>驻守武将与小兵（出征后会从这里抽离）。</summary>
    public List<GeneralInstance> Generals { get; } = new();
    public List<UnitInstance> Units { get; } = new();

    /// <summary>与本地盘有道路相连的地盘 id。</summary>
    public List<string> Adjacent { get; } = new();
}

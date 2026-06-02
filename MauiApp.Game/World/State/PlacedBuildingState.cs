using MauiApp.Game.Model;

namespace MauiApp.Game.World.State;

/// <summary>地盘上的建筑实例（含建造进度）。</summary>
public sealed class PlacedBuildingState
{
    public string TemplateId { get; set; } = "";
    public BuildingTemplate Template { get; set; } = null!;
    public int Level { get; set; } = 1;

    /// <summary>剩余建造月数；0 表示已完工并开始产出。</summary>
    public int RemainingTurns { get; set; }

    public bool IsComplete => RemainingTurns <= 0;
}

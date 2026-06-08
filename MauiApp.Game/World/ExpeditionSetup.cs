using MauiApp.Game.World.State;

namespace MauiApp.Game.World;

/// <summary>玩家出征编队：武将、小兵与携带粮草。</summary>
public sealed class ExpeditionSetup
{
    public required IReadOnlyList<string> GeneralTemplateIds { get; init; }
    public required IReadOnlyList<int> UnitWorldIds { get; init; }
    public int CarriedFood { get; init; }

    public static ExpeditionSetup AllFromTile(TileState tile, int carriedFood = 0) => new()
    {
        GeneralTemplateIds = tile.Generals.Where(g => !g.ActedThisMonth).Select(g => g.TemplateId).ToList(),
        UnitWorldIds = tile.Units.Select(u => u.Id).ToList(),
        CarriedFood = carriedFood,
    };
}

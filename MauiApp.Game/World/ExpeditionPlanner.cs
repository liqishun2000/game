using MauiApp.Game.Content;
using MauiApp.Game.Stats;
using MauiApp.Game.World.State;

namespace MauiApp.Game.World;

/// <summary>出征编队校验与战力估算。</summary>
public static class ExpeditionPlanner
{
    public static OperationResult Validate(
        GameState state, string attackerTileId, ExpeditionSetup setup, BalanceConfig? balance = null)
    {
        var b = balance ?? state.Balance;
        if (!state.Tiles.TryGetValue(attackerTileId, out var tile))
            return OperationResult.Fail("出发地盘无效");

        if (setup.GeneralTemplateIds.Count == 0)
            return OperationResult.Fail("至少选择一名武将");

        var genSet = setup.GeneralTemplateIds.ToHashSet();
        foreach (var id in setup.GeneralTemplateIds)
        {
            var g = tile.Generals.FirstOrDefault(x => x.TemplateId == id);
            if (g is null)
                return OperationResult.Fail("所选武将不在该地盘");
            if (g.ActedThisMonth)
                return OperationResult.Fail($"{g.Template.Name} 本月已行动");
        }

        var unitSet = setup.UnitWorldIds.ToHashSet();
        foreach (var uid in setup.UnitWorldIds)
        {
            if (!tile.Units.Any(u => u.Id == uid))
                return OperationResult.Fail("所选小兵不在该地盘");
        }

        if (setup.CarriedFood < 0)
            return OperationResult.Fail("携带粮草不能为负");

        var faction = state.Factions[tile.OwnerFactionId];
        if (setup.CarriedFood > faction.Food)
            return OperationResult.Fail($"粮草不足（库存 {faction.Food}）");

        return OperationResult.Ok();
    }

    public static int SuggestedFood(int unitCount, BalanceConfig b) =>
        Math.Max(0, unitCount * b.BattleFoodPerUnit * 10);

    public static int EstimatePower(
        IEnumerable<GeneralInstance> generals, IEnumerable<UnitInstance> units,
        ContentDatabase content, BalanceConfig b)
    {
        var gens = generals.ToList();
        var unitList = units.ToList();
        int ts = gens.Count == 0 ? 0 : gens.Max(g => g.Template.MapStats.Tongshuai);
        int total = 0;
        foreach (var g in gens)
        {
            var s = StatCalculator.DeriveGeneralBattleStats(g, content, b);
            total += s.Hp + s.PAtk + s.PDef;
        }

        foreach (var u in unitList)
        {
            var s = StatCalculator.DeriveUnitBattleStats(u, ts, content, b);
            total += s.Hp + s.PAtk;
        }

        return total;
    }
}

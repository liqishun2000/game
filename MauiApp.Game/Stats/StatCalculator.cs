using MauiApp.Game.Model;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Stats;

/// <summary>
/// 数值计算（大地图部分，对应 05-stats-formulas.md 第 10/11 节）。
/// 战场派生/伤害公式在 M4 里程碑补充。
/// </summary>
public static class StatCalculator
{
    /// <summary>驻守政治加成 = 该地最高政治 / 250。</summary>
    public static double PoliticalBonus(TileState tile)
    {
        if (tile.Generals.Count == 0) return 0;
        int maxZ = tile.Generals.Max(g => g.Template.MapStats.Zhengzhi);
        return maxZ / 250.0;
    }

    /// <summary>科技产出加成 = 该地最高 (智力+政治)/2 / 250。</summary>
    public static double TechBonus(TileState tile)
    {
        if (tile.Generals.Count == 0) return 0;
        double best = tile.Generals.Max(g => (g.Template.MapStats.Zhili + g.Template.MapStats.Zhengzhi) / 2.0);
        return best / 250.0;
    }

    public static int GoldProduction(TileState tile) =>
        ResourceProduction(tile, ResourceType.Gold, PoliticalBonus(tile));

    public static int FoodProduction(TileState tile) =>
        ResourceProduction(tile, ResourceType.Food, PoliticalBonus(tile));

    public static int TechProduction(TileState tile) =>
        ResourceProduction(tile, ResourceType.TechPoints, TechBonus(tile));

    private static int ResourceProduction(TileState tile, ResourceType resource, double bonus)
    {
        int baseAmount = 0;
        foreach (var b in tile.Buildings)
        {
            if (!b.IsComplete) continue;
            foreach (var fn in b.Template.Functions)
            {
                if (MatchesResource(fn, resource))
                    baseAmount += fn.AmountPerTurn;
            }
        }

        return (int)Math.Floor(baseAmount * (1 + bonus));
    }

    private static bool MatchesResource(BuildingFunction fn, ResourceType resource)
    {
        // research 功能视为产出科技点
        if (resource == ResourceType.TechPoints
            && string.Equals(fn.Type, "research", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(fn.Type, "produce", StringComparison.OrdinalIgnoreCase))
            return false;

        return resource switch
        {
            ResourceType.Gold => string.Equals(fn.Resource, "gold", StringComparison.OrdinalIgnoreCase),
            ResourceType.Food => string.Equals(fn.Resource, "food", StringComparison.OrdinalIgnoreCase),
            ResourceType.TechPoints => string.Equals(fn.Resource, "techPoints", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    /// <summary>募兵成本：基础成本 * (1 - 主将魅力/400)，折扣上限 25%。</summary>
    public static Cost RecruitUnitCost(UnitTemplate template, int commanderMeili)
    {
        double discount = Math.Min(commanderMeili / 400.0, 0.25);
        double mul = 1 - discount;
        return new Cost
        {
            Gold = (int)Math.Ceiling(template.RecruitCost.Gold * mul),
            Food = (int)Math.Ceiling(template.RecruitCost.Food * mul),
            TechPoints = (int)Math.Ceiling(template.RecruitCost.TechPoints * mul),
        };
    }
}

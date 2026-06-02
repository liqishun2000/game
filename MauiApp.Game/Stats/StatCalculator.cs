using MauiApp.Game.Content;
using MauiApp.Game.Model;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Stats;

/// <summary>
/// 数值计算：大地图产出/募兵（05 第 10/11 节）与战场派生/伤害（05 第 2~6 节）。
/// </summary>
public static class StatCalculator
{
    // ============ 大地图（05 第 10/11 节）============

    public static double PoliticalBonus(TileState tile)
    {
        if (tile.Generals.Count == 0) return 0;
        int maxZ = tile.Generals.Max(g => g.Template.MapStats.Zhengzhi);
        return maxZ / 250.0;
    }

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
                if (MatchesResource(fn, resource))
                    baseAmount += fn.AmountPerTurn;
        }

        return (int)Math.Floor(baseAmount * (1 + bonus));
    }

    private static bool MatchesResource(BuildingFunction fn, ResourceType resource)
    {
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

    // ============ 战场派生（05 第 2~3 节）============

    /// <summary>由武将大地图六维派生战场六维（含装备与特性加成）。</summary>
    public static BattleStats DeriveGeneralBattleStats(GeneralInstance g, ContentDatabase content, BalanceConfig b)
    {
        var m = g.Template.MapStats;
        var s = new BattleStats
        {
            Hp = (int)(b.HpBase + m.Tongshuai * b.HpPerTongshuai + m.Wuli * b.HpPerWuli + g.Level * b.HpPerLevel),
            PAtk = (int)(b.PAtkBase + m.Wuli * b.PAtkPerWuli + m.Tongshuai * b.PAtkPerTongshuai),
            MAtk = (int)(b.MAtkBase + m.Zhili * b.MAtkPerZhili),
            PDef = (int)(b.PDefBase + m.Tongshuai * b.PDefPerTongshuai + m.Wuli * b.PDefPerWuli),
            MDef = (int)(b.MDefBase + m.Zhili * b.MDefPerZhili + m.Yizhi * b.MDefPerYizhi),
            Spd = (int)(b.SpdBase + m.Wuli * b.SpdPerWuli + m.Yizhi * b.SpdPerYizhi),
        };

        ApplyEquipment(s, g.EquipmentId, content);
        ApplyTraits(s, g.Template.Traits);
        return s;
    }

    /// <summary>小兵战场六维：兵种基础值 + 主将统帅领导加成 + 装备。</summary>
    public static BattleStats DeriveUnitBattleStats(
        UnitInstance u, int commanderTongshuai, ContentDatabase content, BalanceConfig b)
    {
        var bs = u.Template.BattleStatsBase;
        double lead = commanderTongshuai / b.UnitTongshuaiDivisor;
        var s = new BattleStats
        {
            Hp = (int)(bs.Hp * (1 + lead)),
            PAtk = (int)(bs.PAtk * (1 + lead)),
            MAtk = bs.MAtk,
            PDef = (int)(bs.PDef * (1 + lead * b.UnitDefTongshuaiFactor)),
            MDef = bs.MDef,
            Spd = bs.Spd,
        };

        ApplyEquipment(s, u.EquipmentId, content);
        ApplyTraits(s, u.Template.Traits);
        return s;
    }

    private static void ApplyEquipment(BattleStats s, string? equipmentId, ContentDatabase content)
    {
        if (equipmentId is null || !content.Equipment.TryGetValue(equipmentId, out var eq)) return;
        var mod = eq.StatMods;
        s.Hp += mod.Hp;
        s.PAtk += mod.PAtk;
        s.MAtk += mod.MAtk;
        s.PDef += mod.PDef;
        s.MDef += mod.MDef;
        s.Spd += mod.Spd;
    }

    private static void ApplyTraits(BattleStats s, IEnumerable<string> traits)
    {
        foreach (var t in traits)
        {
            switch (t)
            {
                case "fenyong": // 奋勇：物功 +10%
                    s.PAtk = (int)(s.PAtk * 1.1);
                    break;
                case "rende": // 仁德：略增生命（士气向，简化为生命 +5%）
                    s.Hp = (int)(s.Hp * 1.05);
                    break;
            }
        }
    }

    // ============ 士气与伤害（05 第 5~6 节）============

    public static double MoraleMultiplier(int morale, BalanceConfig b) =>
        b.MoraleMulBase + b.MoraleMulSpan * (Math.Clamp(morale, 0, 100) / 100.0);

    /// <summary>物理伤害：raw = a^2/(a+d)，再乘技能/士气/克制/随机；防守方 jianren 减伤。</summary>
    public static int PhysicalDamage(
        int pAtk, int pDef, int attackerMorale, double skillMul,
        IReadOnlyCollection<string> defenderTraits, IRandomSource rng, BalanceConfig b,
        double typeMul = 1.0)
    {
        double raw = (double)pAtk * pAtk / Math.Max(1, pAtk + pDef);
        double dmg = raw * skillMul * typeMul * MoraleMultiplier(attackerMorale, b) * RandFactor(rng, b);
        if (defenderTraits.Contains("jianren")) dmg *= 0.9;
        return Math.Max(1, (int)Math.Floor(dmg));
    }

    /// <summary>谋略伤害：raw = m^2/(m+md)，乘技能/士气/随机。</summary>
    public static int StrategyDamage(
        int mAtk, int mDef, int attackerMorale, double skillMul, IRandomSource rng, BalanceConfig b)
    {
        double raw = (double)mAtk * mAtk / Math.Max(1, mAtk + mDef);
        double dmg = raw * skillMul * MoraleMultiplier(attackerMorale, b) * RandFactor(rng, b);
        return Math.Max(1, (int)Math.Floor(dmg));
    }

    private static double RandFactor(IRandomSource rng, BalanceConfig b) =>
        b.DamageRandMin + rng.NextDouble() * (b.DamageRandMax - b.DamageRandMin);

    // ============ 俘获 / 招降（05 第 8~9 节）============

    /// <summary>武将被击败时的俘获概率。带"不被俘获"则恒为 0。</summary>
    public static double CaptureChance(
        bool hasBushoufu, int adjacentEnemies, bool fleeing,
        double remainingAllyRatio, int capturerMeili, int targetYizhi, BalanceConfig b)
    {
        if (hasBushoufu) return 0;

        double chance = b.CaptureBase
                        + b.CapturePerSurround * adjacentEnemies
                        + (fleeing ? b.CaptureFleeingPenalty : 0)
                        + 0.15 * (1 - Math.Clamp(remainingAllyRatio, 0, 1))
                        + Math.Clamp((capturerMeili - targetYizhi) / 200.0, -0.15, 0.15);

        return Math.Clamp(chance, 0.02, 0.95);
    }

    /// <summary>监狱招降成功率（05 第 9 节）。</summary>
    public static double PersuadeChance(
        int persuaderMeili, int targetYizhi, int detainMonths, bool loyalist, double relationBonus, BalanceConfig b)
    {
        double chance = b.PersuadeBase
                        + Math.Clamp((persuaderMeili - targetYizhi) / 150.0, -0.2, 0.4)
                        + Math.Min(b.PersuadePerDetainMonth * detainMonths, 0.3)
                        + relationBonus
                        + (loyalist ? -0.5 : 0);

        return Math.Clamp(chance, 0.0, 0.95);
    }
}

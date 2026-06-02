using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World.State;

namespace MauiApp.Game.World;

/// <summary>
/// 大地图引擎：月回合推进、经济/粮食/科技结算、建造、招兵、逃兵。
/// 详见设计文档 03-world-map.md 与 05-stats-formulas.md（第 10/11 节）。
/// </summary>
public sealed class WorldEngine
{
    private readonly GameState _state;
    private readonly IRandomSource _rng;

    public WorldEngine(GameState state, IRandomSource rng)
    {
        _state = state;
        _rng = rng;
    }

    public GameState State => _state;

    /// <summary>建造建筑（消耗金钱，需若干月完工）。</summary>
    public OperationResult Build(string factionId, string tileId, string buildingId)
    {
        if (!_state.Tiles.TryGetValue(tileId, out var tile))
            return OperationResult.Fail($"地盘不存在: {tileId}");
        if (tile.OwnerFactionId != factionId)
            return OperationResult.Fail("只能在己方地盘建造");
        if (tile.IsRebelFixed)
            return OperationResult.Fail("叛军固定地盘不可建造");
        if (!_state.Content.Buildings.TryGetValue(buildingId, out var template))
            return OperationResult.Fail($"建筑不存在: {buildingId}");

        int existing = tile.Buildings.Count(b => b.TemplateId == buildingId);
        if (existing >= template.MaxPerTile)
            return OperationResult.Fail($"该建筑在此地已达上限({template.MaxPerTile})");

        var faction = _state.Factions[factionId];
        if (!CanAfford(faction, template.Cost))
            return OperationResult.Fail("资源不足");

        Pay(faction, template.Cost);
        tile.Buildings.Add(new PlacedBuildingState
        {
            TemplateId = buildingId,
            Template = template,
            Level = 1,
            RemainingTurns = template.BuildTurns,
        });

        return OperationResult.Ok($"开始建造 {template.Name}（{template.BuildTurns} 个月完工）");
    }

    /// <summary>招募小兵单位（受粮食被动约束 + 特殊兵种上限）。</summary>
    public OperationResult Recruit(string factionId, string tileId, string unitTemplateId, int count)
    {
        if (count <= 0)
            return OperationResult.Fail("招募数量需为正");
        if (!_state.Tiles.TryGetValue(tileId, out var tile))
            return OperationResult.Fail($"地盘不存在: {tileId}");
        if (tile.OwnerFactionId != factionId)
            return OperationResult.Fail("只能在己方地盘招兵");
        if (tile.IsRebelFixed)
            return OperationResult.Fail("叛军固定地盘不可招兵");

        var faction = _state.Factions[factionId];
        if (!faction.Def.RecruitableUnitIds.Contains(unitTemplateId))
            return OperationResult.Fail("该势力无法招募此兵种");
        if (!_state.Content.Units.TryGetValue(unitTemplateId, out var template))
            return OperationResult.Fail($"兵种不存在: {unitTemplateId}");

        if (template.IsSpecial && template.MaxCount is int max)
        {
            int current = _state.CountUnits(factionId, unitTemplateId);
            if (current + count > max)
                return OperationResult.Fail($"特殊兵种 {template.Name} 超出上限({max})，现有 {current}");
        }

        int commanderMeili = tile.Generals.Count == 0 ? 0 : tile.Generals.Max(g => g.Template.MapStats.Meili);
        var unitCost = StatCalculator.RecruitUnitCost(template, commanderMeili);
        var total = new Cost
        {
            Gold = unitCost.Gold * count,
            Food = unitCost.Food * count,
            TechPoints = unitCost.TechPoints * count,
        };

        if (!CanAfford(faction, total))
            return OperationResult.Fail("资源不足");

        Pay(faction, total);
        for (int i = 0; i < count; i++)
        {
            tile.Units.Add(new UnitInstance
            {
                Id = _state.NextUnitId++,
                TemplateId = unitTemplateId,
                Template = template,
                FactionId = factionId,
                CurHp = template.BattleStatsBase.Hp,
                Morale = 100,
                TileId = tileId,
            });
        }

        return OperationResult.Ok($"招募 {template.Name} ×{count}");
    }

    /// <summary>研究科技（消耗科技点/金钱，需满足前置）。</summary>
    public OperationResult Research(string factionId, string techId)
    {
        if (!_state.Content.Techs.TryGetValue(techId, out var tech))
            return OperationResult.Fail($"科技不存在: {techId}");

        var faction = _state.Factions[factionId];
        if (faction.ResearchedTechIds.Contains(techId))
            return OperationResult.Fail("该科技已研究");

        var missing = tech.PrereqIds.Where(p => !faction.ResearchedTechIds.Contains(p)).ToList();
        if (missing.Count > 0)
            return OperationResult.Fail($"前置科技未完成: {string.Join("、", missing)}");

        if (!CanAfford(faction, tech.Cost))
            return OperationResult.Fail("资源不足");

        Pay(faction, tech.Cost);
        faction.ResearchedTechIds.Add(techId);
        return OperationResult.Ok($"已研究 {tech.Name}");
    }

    /// <summary>推进一个月：产出 -> 粮食消耗/逃兵 -> 建造进度 -> 监狱计时。</summary>
    public MonthlyReport AdvanceMonth()
    {
        var report = new MonthlyReport { Month = _state.Month };

        foreach (var faction in _state.Factions.Values)
        {
            // 叛军固定：不产出、不消耗、不逃兵（小兵数固定，05/06）
            if (faction.Kind == FactionKind.Rebel) continue;

            var summary = new FactionMonthSummary { FactionId = faction.Id };

            foreach (var tile in _state.TilesOf(faction.Id))
            {
                summary.GoldGained += StatCalculator.GoldProduction(tile);
                summary.FoodProduced += StatCalculator.FoodProduction(tile);
                summary.TechGained += StatCalculator.TechProduction(tile);
            }

            faction.Gold += summary.GoldGained;
            faction.Food += summary.FoodProduced;
            faction.TechPoints += summary.TechGained;

            ApplyFoodUpkeep(faction, summary);
            report.Factions.Add(summary);
        }

        AdvanceBuildings(report);
        AdvancePrison();

        _state.Month++;
        return report;
    }

    private void ApplyFoodUpkeep(FactionState faction, FactionMonthSummary summary)
    {
        var units = _state.TilesOf(faction.Id).SelectMany(t => t.Units).ToList();
        int upkeep = units.Sum(u => u.Template.FoodUpkeep);
        summary.FoodUpkeep = upkeep;

        faction.Food -= upkeep;
        if (faction.Food >= 0 || upkeep <= 0)
            return;

        // 断粮：逃兵率 = min(0.5, 缺口/总兵食量)，随机减员并降士气（03/05 第 10 节）
        int deficit = -faction.Food;
        double desertRate = Math.Min(0.5, (double)deficit / upkeep);
        int desertCount = (int)Math.Round(units.Count * desertRate);
        desertCount = Math.Min(desertCount, units.Count);

        for (int i = 0; i < desertCount; i++)
        {
            int idx = _rng.Next(units.Count);
            var victim = units[idx];
            units.RemoveAt(idx);
            _state.Tiles[victim.TileId].Units.Remove(victim);
        }

        foreach (var u in units)
            u.Morale = Math.Max(0, u.Morale - 10);

        summary.Deserters = desertCount;
        faction.Food = 0;
    }

    private void AdvanceBuildings(MonthlyReport report)
    {
        foreach (var tile in _state.Tiles.Values)
        {
            foreach (var b in tile.Buildings)
            {
                if (b.IsComplete) continue;
                b.RemainingTurns--;
                if (b.IsComplete)
                    report.CompletedBuildings.Add($"{tile.Name}: {b.Template.Name}");
            }
        }
    }

    private void AdvancePrison()
    {
        foreach (var faction in _state.Factions.Values)
            foreach (var prisoner in faction.Prison)
                prisoner.DetainedMonths++;
    }

    private static bool CanAfford(FactionState f, Cost cost) =>
        f.Gold >= cost.Gold && f.Food >= cost.Food && f.TechPoints >= cost.TechPoints;

    private static void Pay(FactionState f, Cost cost)
    {
        f.Gold -= cost.Gold;
        f.Food -= cost.Food;
        f.TechPoints -= cost.TechPoints;
    }
}

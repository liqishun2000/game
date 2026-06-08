using MauiApp.Game.Battle;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World.State;

namespace MauiApp.Game.World;

/// <summary>一场已发起、待进行/已结束的战斗及其回写所需上下文。</summary>
public sealed class PendingBattle
{
    public required BattleEngine Engine { get; init; }
    public required string AttackerTileId { get; init; }
    public required string DefenderTileId { get; init; }
    public required string AttackerFactionId { get; init; }
    public required string DefenderFactionId { get; init; }
    public required List<GeneralInstance> AttackerGenerals { get; init; }
    public required List<UnitInstance> AttackerUnits { get; init; }
    public required List<GeneralInstance> DefenderGenerals { get; init; }
    public required List<UnitInstance> DefenderUnits { get; init; }

    /// <summary>玩家进攻时 true：进入战斗页先布阵再开战。</summary>
    public bool AwaitDeployment { get; init; }
}

/// <summary>
/// 出征编排：构造战斗、并在战斗结束后把结果回写大地图（占领/伤亡/俘获/掉落）。
/// 对应 03-world-map.md 第 8 节与 04-battle.md 第 10 节。
/// </summary>
public sealed class WarService
{
    private readonly GameState _state;
    private readonly IRandomSource _rng;

    public WarService(GameState state, IRandomSource rng)
    {
        _state = state;
        _rng = rng;
    }

    /// <summary>校验相邻并构造一场战斗（防守方该地全部驻军上阵）。</summary>
    public PendingBattle CreateBattle(
        string attackerTileId, string defenderTileId,
        IEnumerable<string> generalTemplateIds, IEnumerable<int> unitWorldIds,
        int attackerFood = 0, int? defenderFood = null, bool awaitDeployment = false)
    {
        var atkTile = _state.Tiles[attackerTileId];
        var defTile = _state.Tiles[defenderTileId];
        if (!atkTile.Adjacent.Contains(defenderTileId))
            throw new InvalidOperationException("目标地盘不相邻");

        var genIds = generalTemplateIds.ToHashSet();
        var unitIds = unitWorldIds.ToHashSet();

        var atkGenerals = atkTile.Generals.Where(g => genIds.Contains(g.TemplateId)).ToList();
        var atkUnits = atkTile.Units.Where(u => unitIds.Contains(u.Id)).ToList();
        if (atkGenerals.Count == 0)
            throw new InvalidOperationException("出征必须至少携带一名武将");

        var defGenerals = defTile.Generals.ToList();
        var defUnits = defTile.Units.ToList();

        var atkFactionId = atkTile.OwnerFactionId;
        var defFactionId = defTile.OwnerFactionId;

        var playerSide = _state.Factions[atkFactionId].Kind == FactionKind.Player
            ? BattleSide.Attacker
            : BattleSide.Defender;

        var map = _state.Content.Maps[_state.MapId];
        var battleCfg = map.BattleConfig ?? BattleConfig.Default50;
        int terrainSeed = _state.Seed ^ HashCode.Combine(attackerTileId, defenderTileId, _state.Month);

        var battle = BattleFactory.CreateBattle(
            _state.Content,
            new BattleFactory.Side { FactionId = atkFactionId, Generals = atkGenerals, Units = atkUnits },
            new BattleFactory.Side { FactionId = defFactionId, Generals = defGenerals, Units = defUnits },
            playerSide,
            battleCfg,
            terrainSeed,
            _state.Balance);

        int atkUnitCount = atkGenerals.Count + atkUnits.Count;
        int defUnitCount = defGenerals.Count + defUnits.Count;
        battle.SideFood[BattleSide.Attacker] = attackerFood;
        battle.SideFood[BattleSide.Defender] = defenderFood ?? ResolveDefenderFood(defFactionId, defUnitCount);

        var engine = new BattleEngine(battle, _rng, _state.Balance);

        return new PendingBattle
        {
            Engine = engine,
            AttackerTileId = attackerTileId,
            DefenderTileId = defenderTileId,
            AttackerFactionId = atkFactionId,
            DefenderFactionId = defFactionId,
            AttackerGenerals = atkGenerals,
            AttackerUnits = atkUnits,
            DefenderGenerals = defGenerals,
            DefenderUnits = defUnits,
            AwaitDeployment = awaitDeployment,
        };
    }

    private int ResolveDefenderFood(string defFactionId, int unitCount)
    {
        if (!_state.Factions.TryGetValue(defFactionId, out var faction)) return 0;
        int perUnit = _state.Balance.BattleFoodPerUnit * 15;
        return Math.Min(faction.Food, unitCount * perUnit);
    }

    /// <summary>从势力粮库扣除战斗携带粮草。</summary>
    public void CommitBattleFood(PendingBattle pending)
    {
        DeductFood(pending.AttackerFactionId, pending.Engine.State.SideFood.GetValueOrDefault(BattleSide.Attacker));
        DeductFood(pending.DefenderFactionId, pending.Engine.State.SideFood.GetValueOrDefault(BattleSide.Defender));
    }

    private void DeductFood(string factionId, int amount)
    {
        if (amount <= 0 || !_state.Factions.TryGetValue(factionId, out var faction)) return;
        faction.Food = Math.Max(0, faction.Food - amount);
    }

    /// <summary>把战斗结果回写大地图。需在战斗结束后调用。</summary>
    public void ApplyResult(PendingBattle pending)
    {
        var result = pending.Engine.Result;
        if (!result.Finished) throw new InvalidOperationException("战斗尚未结束");

        var state = pending.Engine.State;
        var atkTile = _state.Tiles[pending.AttackerTileId];
        var defTile = _state.Tiles[pending.DefenderTileId];

        // 1) 阵亡小兵移除（按 WorldUnitId 映射）
        var deadUnitIds = state.Units
            .Where(u => !u.IsGeneral && !u.IsAlive && u.WorldUnitId is not null)
            .Select(u => u.WorldUnitId!.Value)
            .ToHashSet();
        RemoveUnits(atkTile, pending.AttackerUnits, deadUnitIds);
        RemoveUnits(defTile, pending.DefenderUnits, deadUnitIds);

        // 2) 武将俘获/阵亡/逃脱
        HandleGenerals(pending, result);

        // 3) 装备掉落入库
        foreach (var drop in result.Drops)
        {
            var faction = drop.ToSide == BattleSide.Attacker ? pending.AttackerFactionId : pending.DefenderFactionId;
            _state.Factions[faction].Armory.Add(drop.EquipmentId);
        }

        // 4) 占领：进攻方胜则防守地盘易主，存活进攻部队推进
        if (result.AttackerWon)
            Occupy(atkTile, defTile, pending);
    }

    private void HandleGenerals(PendingBattle pending, BattleResult result)
    {
        var atkTile = _state.Tiles[pending.AttackerTileId];
        var defTile = _state.Tiles[pending.DefenderTileId];

        foreach (var cap in result.Captured)
        {
            var (g, tile) = FindGeneral(pending, cap.GeneralTemplateId);
            if (g is null) continue;
            tile?.Generals.Remove(g);
            g.Status = GeneralStatus.Captured;
            g.TileId = null;
            var captorFactionId = cap.CapturedBy == BattleSide.Attacker ? pending.AttackerFactionId : pending.DefenderFactionId;
            _state.Factions[captorFactionId].Prison.Add(g);
        }

        foreach (var killedId in result.KilledGenerals)
        {
            var (g, tile) = FindGeneral(pending, killedId);
            if (g is null) continue;
            tile?.Generals.Remove(g);
            g.Status = GeneralStatus.Dead;
            g.TileId = null;
        }
        // 逃脱武将保留在原地（status 仍 Active）
    }

    private (GeneralInstance? General, TileState? Tile) FindGeneral(PendingBattle pending, string templateId)
    {
        var atkTile = _state.Tiles[pending.AttackerTileId];
        var defTile = _state.Tiles[pending.DefenderTileId];
        var g = pending.AttackerGenerals.FirstOrDefault(x => x.TemplateId == templateId);
        if (g is not null) return (g, atkTile);
        g = pending.DefenderGenerals.FirstOrDefault(x => x.TemplateId == templateId);
        return g is not null ? (g, defTile) : (null, null);
    }

    private static void RemoveUnits(TileState tile, List<UnitInstance> participants, HashSet<int> deadIds)
    {
        foreach (var u in participants.Where(u => deadIds.Contains(u.Id)))
            tile.Units.Remove(u);
    }

    private void Occupy(TileState atkTile, TileState defTile, PendingBattle pending)
    {
        defTile.OwnerFactionId = pending.AttackerFactionId;
        defTile.IsRebelFixed = false;

        // 存活的进攻方武将与小兵推进到占领地
        var survivingGenerals = pending.AttackerGenerals
            .Where(g => g.Status == GeneralStatus.Active && atkTile.Generals.Contains(g)).ToList();
        foreach (var g in survivingGenerals)
        {
            atkTile.Generals.Remove(g);
            g.TileId = defTile.Id;
            defTile.Generals.Add(g);
        }

        var survivingUnits = pending.AttackerUnits.Where(u => atkTile.Units.Contains(u)).ToList();
        foreach (var u in survivingUnits)
        {
            atkTile.Units.Remove(u);
            u.TileId = defTile.Id;
            u.FactionId = pending.AttackerFactionId;
            defTile.Units.Add(u);
        }
    }
}

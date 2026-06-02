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
        IEnumerable<string> generalTemplateIds, IEnumerable<int> unitWorldIds)
    {
        var atkTile = _state.Tiles[attackerTileId];
        var defTile = _state.Tiles[defenderTileId];
        if (!atkTile.Adjacent.Contains(defenderTileId))
            throw new InvalidOperationException("目标地盘不相邻");

        var genIds = generalTemplateIds.ToHashSet();
        var unitIds = unitWorldIds.ToHashSet();

        var atkGenerals = atkTile.Generals.Where(g => genIds.Contains(g.TemplateId)).ToList();
        var atkUnits = atkTile.Units.Where(u => unitIds.Contains(u.Id)).ToList();
        var defGenerals = defTile.Generals.ToList();
        var defUnits = defTile.Units.ToList();

        var atkFactionId = atkTile.OwnerFactionId;
        var defFactionId = defTile.OwnerFactionId;

        var playerSide = _state.Factions[atkFactionId].Kind == FactionKind.Player
            ? BattleSide.Attacker
            : BattleSide.Defender;

        var battle = BattleFactory.CreateBattle(
            _state.Content,
            new BattleFactory.Side { FactionId = atkFactionId, Generals = atkGenerals, Units = atkUnits },
            new BattleFactory.Side { FactionId = defFactionId, Generals = defGenerals, Units = defUnits },
            playerSide,
            balance: _state.Balance);

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
        };
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

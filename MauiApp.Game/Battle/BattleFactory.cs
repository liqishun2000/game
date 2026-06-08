using MauiApp.Game.Content;
using MauiApp.Game.Stats;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Battle;

/// <summary>由武将与小兵实例构造一场战斗（含简单自动布阵）。</summary>
public static class BattleFactory
{
    public const int GeneralMove = 4;
    public const int DefaultSpawnDepth = 8;

    public sealed class Side
    {
        public string FactionId { get; init; } = "";
        public List<GeneralInstance> Generals { get; init; } = new();
        public List<UnitInstance> Units { get; init; } = new();
    }

    public static BattleState CreateBattle(
        ContentDatabase content,
        Side attacker,
        Side defender,
        BattleSide playerSide = BattleSide.Attacker,
        BattleConfig? config = null,
        int terrainSeed = 0,
        BalanceConfig? balance = null)
    {
        var cfg = config ?? BattleConfig.Default50;
        var b = balance ?? BalanceConfig.Default;
        int depth = Math.Min(DefaultSpawnDepth, cfg.Width / 6);
        var state = new BattleState { Width = cfg.Width, Height = cfg.Height, PlayerSide = playerSide, SpawnDepth = depth };
        state.Terrain = BattleTerrainGenerator.Generate(cfg.Width, cfg.Height, terrainSeed, cfg.TerrainMode);

        int idSeq = 1;
        int attackerTs = attacker.Generals.Count == 0 ? 0 : attacker.Generals.Max(g => g.Template.MapStats.Tongshuai);
        int defenderTs = defender.Generals.Count == 0 ? 0 : defender.Generals.Max(g => g.Template.MapStats.Tongshuai);

        var attackerUnits = BuildSideUnits(content, b, attacker, BattleSide.Attacker, attackerTs, ref idSeq);
        var defenderUnits = BuildSideUnits(content, b, defender, BattleSide.Defender, defenderTs, ref idSeq);
        Deploy(attackerUnits, SpawnZone(state, left: true, depth), state);
        Deploy(defenderUnits, SpawnZone(state, left: false, depth), state);

        state.Units.AddRange(attackerUnits);
        state.Units.AddRange(defenderUnits);
        return state;
    }

    private static IEnumerable<(int Col, int Row)> SpawnZone(BattleState state, bool left, int depth)
    {
        int c0 = left ? 0 : state.Width - depth;
        int c1 = left ? depth : state.Width;
        for (int c = c0; c < c1; c++)
        for (int r = 0; r < state.Height; r++)
            if (BattleState.IsPassable(state.GetTerrain(c, r)))
                yield return (c, r);
    }

    private static List<BattleUnit> BuildSideUnits(
        ContentDatabase content, BalanceConfig b, Side side, BattleSide which, int commanderTs, ref int idSeq)
    {
        var list = new List<BattleUnit>();

        foreach (var g in side.Generals)
        {
            var stats = StatCalculator.DeriveGeneralBattleStats(g, content, b);
            bool droppable = g.EquipmentId is not null
                             && content.Equipment.TryGetValue(g.EquipmentId, out var eq)
                             && eq.Droppable;
            list.Add(new BattleUnit
            {
                Id = idSeq++,
                Side = which,
                FactionId = side.FactionId,
                Name = g.Template.Name,
                IsGeneral = true,
                GeneralTemplateId = g.TemplateId,
                EquipmentId = g.EquipmentId,
                EquipmentDroppable = droppable,
                Meili = g.Template.MapStats.Meili,
                Yizhi = g.Template.MapStats.Yizhi,
                Stats = stats,
                MaxHp = stats.Hp,
                CurHp = stats.Hp,
                Move = GeneralMove,
                Traits = new List<string>(g.Template.Traits),
                ThreatValue = 100,
            });
        }

        foreach (var u in side.Units)
        {
            var stats = StatCalculator.DeriveUnitBattleStats(u, commanderTs, content, b);
            list.Add(new BattleUnit
            {
                Id = idSeq++,
                Side = which,
                FactionId = side.FactionId,
                Name = u.Template.Name,
                IsGeneral = false,
                WorldUnitId = u.Id,
                Stats = stats,
                MaxHp = stats.Hp,
                CurHp = stats.Hp,
                Move = u.Template.Move,
                Traits = new List<string>(u.Template.Traits),
                ThreatValue = u.Template.IsSpecial ? 40 : 10,
            });
        }

        return list;
    }

    private static void Deploy(List<BattleUnit> units, IEnumerable<(int Col, int Row)> positions, BattleState state)
    {
        using var pos = positions.GetEnumerator();
        foreach (var u in units)
        {
            while (pos.MoveNext())
            {
                if (state.UnitAt(pos.Current.Col, pos.Current.Row) is not null) continue;
                u.Col = pos.Current.Col;
                u.Row = pos.Current.Row;
                break;
            }
        }
    }

    /// <summary>将指定阵营单位重置为默认出生区布阵。</summary>
    public static void AutoDeploySide(BattleState state, BattleSide side)
    {
        int depth = state.EffectiveSpawnDepth();
        bool left = side == BattleSide.Attacker;
        var zone = SpawnZone(state, left, depth).ToList();
        var units = state.Units.Where(u => u.IsAlive && u.Side == side).ToList();
        Deploy(units, zone, state);
    }
}

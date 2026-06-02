using MauiApp.Game.Content;
using MauiApp.Game.Stats;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Battle;

/// <summary>由武将与小兵实例构造一场战斗（含简单自动布阵）。</summary>
public static class BattleFactory
{
    public const int GeneralMove = 4;

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
        int width = 10,
        int height = 8,
        BalanceConfig? balance = null)
    {
        var b = balance ?? BalanceConfig.Default;
        var state = new BattleState { Width = width, Height = height, PlayerSide = playerSide };
        int idSeq = 1;

        int attackerTs = attacker.Generals.Count == 0 ? 0 : attacker.Generals.Max(g => g.Template.MapStats.Tongshuai);
        int defenderTs = defender.Generals.Count == 0 ? 0 : defender.Generals.Max(g => g.Template.MapStats.Tongshuai);

        var attackerUnits = BuildSideUnits(content, b, attacker, BattleSide.Attacker, attackerTs, ref idSeq);
        var defenderUnits = BuildSideUnits(content, b, defender, BattleSide.Defender, defenderTs, ref idSeq);

        Deploy(attackerUnits, LeftPositions(width, height), state);
        Deploy(defenderUnits, RightPositions(width, height), state);

        state.Units.AddRange(attackerUnits);
        state.Units.AddRange(defenderUnits);
        return state;
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

    private static void Deploy(List<BattleUnit> units, IEnumerable<(int Col, int Row)> positions, BattleState _)
    {
        using var pos = positions.GetEnumerator();
        foreach (var u in units)
        {
            if (!pos.MoveNext()) break;
            u.Col = pos.Current.Col;
            u.Row = pos.Current.Row;
        }
    }

    private static IEnumerable<(int, int)> LeftPositions(int width, int height)
    {
        for (int c = 0; c < width; c++)
            for (int r = 0; r < height; r++)
                yield return (c, r);
    }

    private static IEnumerable<(int, int)> RightPositions(int width, int height)
    {
        for (int c = width - 1; c >= 0; c--)
            for (int r = 0; r < height; r++)
                yield return (c, r);
    }
}

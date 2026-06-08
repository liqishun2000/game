using MauiApp.Game.Battle;
using MauiApp.Game.Model;

namespace MauiApp.Game.Ai;

/// <summary>
/// 战斗 AI（难度分级，04-battle.md 第 11 节）：
/// easy = 就近移动+攻击；normal = 评分集火/优先击杀；hard = 额外避免被包围 + 残血武将撤退。
/// </summary>
public sealed class BattleAi : IAiController
{
    private const double KillBonus = 1000;

    private readonly AiDifficulty _difficulty;

    public BattleAi(AiDifficulty difficulty) => _difficulty = difficulty;

    public UnitTurn DecideTurn(BattleEngine engine, BattleUnit unit)
    {
        if (_difficulty == AiDifficulty.Easy)
            return engine.BuildAutoTurn(unit);

        var state = engine.State;
        var enemies = state.Units.Where(u => u.IsAlive && u.Side != unit.Side).ToList();
        if (enemies.Count == 0) return UnitTurn.Wait();

        // 残血武将向己方入场边缘撤退（到达后可离场）
        if (unit.IsGeneral && unit.CurHp < unit.MaxHp * (_difficulty == AiDifficulty.Hard ? 0.35 : 0.2))
        {
            var retreat = TryRetreatToExit(engine, unit);
            if (retreat is not null) return retreat;
        }

        var reachable = engine.GetReachable(unit);
        UnitTurn? bestTurn = null;
        double bestScore = double.NegativeInfinity;

        foreach (var e in enemies)
        {
            foreach (var cell in reachable)
            {
                if (Manhattan(cell.Col, cell.Row, e.Col, e.Row) != 1) continue;

                int est = engine.EstimateDamage(unit, e);
                double score = est;
                if (est >= e.CurHp) score += KillBonus;
                score += TargetWeight() * e.ThreatValue;
                if (_difficulty == AiDifficulty.Hard)
                    score -= 2.0 * engine.EnemyAdjacentCount(unit.Side, cell.Col, cell.Row);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTurn = (cell.Col == unit.Col && cell.Row == unit.Row)
                        ? UnitTurn.Attack(e.Id)
                        : UnitTurn.MoveAndAttack(cell.Col, cell.Row, e.Id);
                }
            }
        }

        return bestTurn ?? MoveTowardNearest(engine, unit, enemies, reachable);
    }

    private double TargetWeight() => _difficulty == AiDifficulty.Hard ? 0.5 : 0.3;

    private static UnitTurn MoveTowardNearest(
        BattleEngine engine, BattleUnit unit, List<BattleUnit> enemies, HashSet<(int Col, int Row)> reachable)
    {
        var target = enemies.OrderBy(e => Manhattan(unit.Col, unit.Row, e.Col, e.Row)).First();
        var best = reachable
            .OrderBy(p => Manhattan(p.Col, p.Row, target.Col, target.Row))
            .ThenBy(p => p.Col).ThenBy(p => p.Row)
            .First();
        return (best.Col == unit.Col && best.Row == unit.Row)
            ? UnitTurn.Wait()
            : UnitTurn.MoveOnly(best.Col, best.Row);
    }

    private static UnitTurn? TryRetreatToExit(BattleEngine engine, BattleUnit unit)
    {
        var state = engine.State;
        if (state.CanRetreat(unit))
        {
            unit.IsFleeing = true;
            return UnitTurn.RetreatFromBattle();
        }

        var reachable = engine.GetReachable(unit);
        var exitCells = reachable.Where(p => state.IsExitTile(unit.Side, p.Col, p.Row)).ToList();
        if (exitCells.Count == 0)
        {
            int depth = state.EffectiveSpawnDepth();
            var targetCol = unit.Side == BattleSide.Attacker ? depth - 1 : state.Width - depth;
            var toward = reachable
                .OrderBy(p => Math.Abs(p.Col - targetCol) + Math.Abs(p.Row - unit.Row))
                .ThenBy(p => p.Col).ThenBy(p => p.Row)
                .FirstOrDefault();
            if (toward == default || (toward.Col == unit.Col && toward.Row == unit.Row))
                return null;
            unit.IsFleeing = true;
            return UnitTurn.MoveOnly(toward.Col, toward.Row);
        }

        var best = exitCells
            .OrderBy(p => unit.Side == BattleSide.Attacker ? p.Col : -p.Col)
            .ThenBy(p => p.Row)
            .First();
        if (best.Col == unit.Col && best.Row == unit.Row)
            return null;

        unit.IsFleeing = true;
        return UnitTurn.MoveOnly(best.Col, best.Row);
    }

    private static int Manhattan(int c1, int r1, int c2, int r2) =>
        Math.Abs(c1 - c2) + Math.Abs(r1 - r2);
}

using MauiApp.Game.Ai;
using MauiApp.Game.Stats;

namespace MauiApp.Game.Battle;

/// <summary>
/// 战斗引擎：方格战场、按速度行动序、移动寻路、攻击/反击、士气、30 回合上限、胜负判定。
/// 详见 04-battle.md。俘获判定在 M7 接入；战斗 AI 在 M5 用 IAiController 替换内置启发式。
/// </summary>
public sealed class BattleEngine
{
    private readonly BattleState _state;
    private readonly IRandomSource _rng;
    private readonly BalanceConfig _balance;
    private readonly BattleResult _result = new();
    private readonly Dictionary<BattleSide, IAiController> _controllers = new();

    public BattleEngine(BattleState state, IRandomSource rng, BalanceConfig? balance = null)
    {
        _state = state;
        _rng = rng;
        _balance = balance ?? BalanceConfig.Default;
        _state.MaxRounds = _balance.BattleMaxRounds;
    }

    public BattleState State => _state;
    public BattleResult Result => _result;
    public BalanceConfig Balance => _balance;

    /// <summary>为某阵营设置 AI 决策器；自动行动（快进/快速回合）将优先使用它。</summary>
    public void SetController(BattleSide side, IAiController controller) => _controllers[side] = controller;

    private UnitTurn DecideAuto(BattleUnit unit) =>
        _controllers.TryGetValue(unit.Side, out var ai) ? ai.DecideTurn(this, unit) : BuildAutoTurn(unit);

    /// <summary>开始战斗（生成第一回合行动序）。</summary>
    public void Start()
    {
        _state.InitialSoldierCount[BattleSide.Attacker] = _state.AliveOf(BattleSide.Attacker).Count(u => !u.IsGeneral);
        _state.InitialSoldierCount[BattleSide.Defender] = _state.AliveOf(BattleSide.Defender).Count(u => !u.IsGeneral);
        _state.Round = 0;
        BeginRound();
    }

    /// <summary>当前应行动的单位；null 表示本场已结束。</summary>
    public BattleUnit? CurrentUnit()
    {
        PrunePending();
        if (_result.Finished) return null;
        if (_state.PendingOrder.Count == 0) return null;
        return _state.GetUnit(_state.PendingOrder[0]);
    }

    public bool IsFinished(out BattleResult result)
    {
        result = _result;
        return _result.Finished;
    }

    /// <summary>执行当前单位的一个完整回合（移动 + 主行动）。</summary>
    public bool ExecuteTurn(UnitTurn turn)
    {
        var unit = CurrentUnit();
        if (unit is null) return false;

        if (turn.MoveTo is { } dest && (dest.Col != unit.Col || dest.Row != unit.Row))
        {
            if (!GetReachable(unit).Contains(dest))
                return false;
            unit.Col = dest.Col;
            unit.Row = dest.Row;
            unit.HasMovedThisTurn = true;
        }

        if (turn.AttackTargetId is { } targetId)
        {
            var target = _state.GetUnit(targetId);
            if (target is null || !target.IsAlive || target.Side == unit.Side)
                return false;
            if (Manhattan(unit, target) != 1)
                return false;
            ResolveAttack(unit, target);
        }

        AdvanceAfterAction(unit);
        return true;
    }

    /// <summary>快进到下一个我方待决单位：自动替敌方（及空场）行动，遇到玩家单位停下。</summary>
    public void SkipToNextPlayerDecision()
    {
        while (true)
        {
            var unit = CurrentUnit();
            if (unit is null) return;
            if (unit.Side == _state.PlayerSide) return;
            ExecuteTurn(DecideAuto(unit));
        }
    }

    /// <summary>快速回合：本回合所有单位（含我方）自动行动后停在下一回合开始。</summary>
    public void FastResolveTurn()
    {
        int round = _state.Round;
        while (!_result.Finished && _state.Round == round)
        {
            var unit = CurrentUnit();
            if (unit is null) break;
            ExecuteTurn(DecideAuto(unit));
        }
    }

    /// <summary>快速到底：连续自动直到分出胜负或超时。</summary>
    public BattleResult FastResolveAll()
    {
        int guard = 0;
        while (!_result.Finished && guard++ < 100000)
        {
            var unit = CurrentUnit();
            if (unit is null) break;
            ExecuteTurn(DecideAuto(unit));
        }

        return _result;
    }

    // ============ 内部 ============

    private void BeginRound()
    {
        if (_result.Finished) return;

        _state.Round++;
        _state.PendingOrder.Clear();

        var ordered = _state.Units
            .Where(u => u.IsAlive)
            .OrderByDescending(u => u.Stats.Spd)
            .ThenByDescending(u => u.IsGeneral)
            .ThenBy(u => u.Id);

        foreach (var u in ordered)
        {
            u.ActionsLeft = 1;
            u.HasMovedThisTurn = false;
            _state.PendingOrder.Add(u.Id);
        }
    }

    private void AdvanceAfterAction(BattleUnit unit)
    {
        unit.ActionsLeft--;
        if (unit.ActionsLeft <= 0)
            _state.PendingOrder.Remove(unit.Id);

        if (CheckOutcome()) return;

        PrunePending();
        if (_state.PendingOrder.Count == 0)
            EndRound();
    }

    private void EndRound()
    {
        if (CheckOutcome()) return;

        if (_state.Round >= _state.MaxRounds)
        {
            _result.Outcome = BattleOutcome.Timeout;
            _result.Rounds = _state.Round;
            return;
        }

        BeginRound();
    }

    private void PrunePending() =>
        _state.PendingOrder.RemoveAll(id => _state.GetUnit(id) is not { IsAlive: true });

    private bool CheckOutcome()
    {
        if (_result.Finished) return true;

        bool attackerAlive = _state.AliveOf(BattleSide.Attacker).Any();
        bool defenderAlive = _state.AliveOf(BattleSide.Defender).Any();

        if (!attackerAlive || !defenderAlive)
        {
            _result.Outcome = !attackerAlive && !defenderAlive
                ? BattleOutcome.DefenderWins // 同归于尽判防守方守住
                : attackerAlive ? BattleOutcome.AttackerWins : BattleOutcome.DefenderWins;
            _result.Rounds = _state.Round;
            return true;
        }

        return false;
    }

    private void ResolveAttack(BattleUnit attacker, BattleUnit target)
    {
        int dmg = ComputeDamage(attacker, target);
        ApplyDamage(target, dmg);

        // 反击：目标存活且相邻则反击（近战）
        if (target.IsAlive && Manhattan(attacker, target) == 1 && attacker.IsAlive)
        {
            int counter = (int)(ComputeDamage(target, attacker, _balance.CounterFactor));
            ApplyDamage(attacker, counter);
        }
    }

    private int ComputeDamage(BattleUnit attacker, BattleUnit target, double skillMul = 1.0)
    {
        bool useStrategy = attacker.Stats.MAtk > attacker.Stats.PAtk;
        return useStrategy
            ? StatCalculator.StrategyDamage(attacker.Stats.MAtk, target.Stats.MDef, attacker.Morale, skillMul, _rng, _balance)
            : StatCalculator.PhysicalDamage(attacker.Stats.PAtk, target.Stats.PDef, attacker.Morale, skillMul, target.Traits, _rng, _balance);
    }

    private void ApplyDamage(BattleUnit target, int dmg)
    {
        target.CurHp -= dmg;
        if (target.CurHp > 0) return;

        target.CurHp = 0;
        if (!_result.Fallen.Contains(target.Id))
            _result.Fallen.Add(target.Id);

        if (target.IsGeneral)
            HandleGeneralDown(target);
    }

    /// <summary>武将被击败：俘获判定（05 第 8 节）+ 装备掉落。</summary>
    private void HandleGeneralDown(BattleUnit general)
    {
        var capturerSide = general.Side == BattleSide.Attacker ? BattleSide.Defender : BattleSide.Attacker;
        bool bushoufu = general.Traits.Contains("bushoufu");

        int adjacentEnemies = EnemyAdjacentCount(general.Side, general.Col, general.Row);
        int initialSoldiers = _state.InitialSoldierCount.GetValueOrDefault(general.Side, 0);
        int aliveSoldiers = _state.AliveOf(general.Side).Count(u => !u.IsGeneral);
        double ratio = initialSoldiers > 0 ? (double)aliveSoldiers / initialSoldiers : 1.0;
        int capturerMeili = _state.AliveOf(capturerSide).Where(u => u.IsGeneral)
            .Select(u => u.Meili).DefaultIfEmpty(0).Max();

        double chance = StatCalculator.CaptureChance(
            bushoufu, adjacentEnemies, general.IsFleeing, ratio, capturerMeili, general.Yizhi, _balance);

        var templateId = general.GeneralTemplateId!;
        if (_rng.NextDouble() < chance)
            _result.Captured.Add(new CapturedGeneral { GeneralTemplateId = templateId, CapturedBy = capturerSide });
        else if (bushoufu)
            _result.EscapedGenerals.Add(templateId);
        else
            _result.KilledGenerals.Add(templateId);

        if (general.EquipmentDroppable && general.EquipmentId is not null && _rng.NextDouble() < 0.5)
            _result.Drops.Add(new DroppedEquipment { EquipmentId = general.EquipmentId, ToSide = capturerSide });
    }

    /// <summary>BFS 计算单位可达格（不含被占据格，含原地）。</summary>
    public HashSet<(int Col, int Row)> GetReachable(BattleUnit unit)
    {
        var result = new HashSet<(int, int)> { (unit.Col, unit.Row) };
        var dist = new Dictionary<(int, int), int> { [(unit.Col, unit.Row)] = 0 };
        var queue = new Queue<(int, int)>();
        queue.Enqueue((unit.Col, unit.Row));

        int[] dc = { 1, -1, 0, 0 };
        int[] dr = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var (c, r) = queue.Dequeue();
            int d = dist[(c, r)];
            if (d >= unit.Move) continue;

            for (int i = 0; i < 4; i++)
            {
                var nc = c + dc[i];
                var nr = r + dr[i];
                if (!_state.InBounds(nc, nr)) continue;
                if (dist.ContainsKey((nc, nr))) continue;
                if (_state.UnitAt(nc, nr) is not null) continue; // 被占据不可进入

                dist[(nc, nr)] = d + 1;
                result.Add((nc, nr));
                queue.Enqueue((nc, nr));
            }
        }

        return result;
    }

    /// <summary>不消耗随机源的伤害估计（随机因子取 1.0），供 AI 评估使用。</summary>
    public int EstimateDamage(BattleUnit attacker, BattleUnit target, double skillMul = 1.0)
    {
        double mm = StatCalculator.MoraleMultiplier(attacker.Morale, _balance);
        bool useStrategy = attacker.Stats.MAtk > attacker.Stats.PAtk;
        double raw = useStrategy
            ? (double)attacker.Stats.MAtk * attacker.Stats.MAtk / Math.Max(1, attacker.Stats.MAtk + target.Stats.MDef)
            : (double)attacker.Stats.PAtk * attacker.Stats.PAtk / Math.Max(1, attacker.Stats.PAtk + target.Stats.PDef);
        double dmg = raw * skillMul * mm;
        if (!useStrategy && target.Traits.Contains("jianren")) dmg *= 0.9;
        return Math.Max(1, (int)Math.Floor(dmg));
    }

    /// <summary>某格周围相邻的敌方单位数（用于 AI 评估暴露/包围）。</summary>
    public int EnemyAdjacentCount(BattleSide side, int col, int row) =>
        _state.Units.Count(u => u.IsAlive && u.Side != side &&
                                Math.Abs(u.Col - col) + Math.Abs(u.Row - row) == 1);

    /// <summary>内置启发式（easy 级别）：走向最近敌人并在相邻时攻击。M5 用 AI 替换。</summary>
    public UnitTurn BuildAutoTurn(BattleUnit unit)
    {
        var enemy = _state.Units
            .Where(u => u.IsAlive && u.Side != unit.Side)
            .OrderBy(u => Manhattan(unit, u))
            .FirstOrDefault();

        if (enemy is null) return UnitTurn.Wait();

        if (Manhattan(unit, enemy) == 1)
            return UnitTurn.Attack(enemy.Id);

        var reachable = GetReachable(unit);
        var best = reachable
            .OrderBy(p => Math.Abs(p.Col - enemy.Col) + Math.Abs(p.Row - enemy.Row))
            .ThenBy(p => p.Col).ThenBy(p => p.Row)
            .First();

        bool adjacentAfter = Math.Abs(best.Col - enemy.Col) + Math.Abs(best.Row - enemy.Row) == 1;
        return adjacentAfter
            ? UnitTurn.MoveAndAttack(best.Col, best.Row, enemy.Id)
            : UnitTurn.MoveOnly(best.Col, best.Row);
    }

    private static int Manhattan(BattleUnit a, BattleUnit b) =>
        Math.Abs(a.Col - b.Col) + Math.Abs(a.Row - b.Row);
}

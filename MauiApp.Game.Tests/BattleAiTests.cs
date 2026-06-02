using MauiApp.Game.Ai;
using MauiApp.Game.Battle;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;

namespace MauiApp.Game.Tests;

public class BattleAiTests
{
    private static BattleUnit U(int id, BattleSide side, int col, int row, int spd, int move, int hp, int curHp, int pAtk, int pDef, bool general = false)
        => new()
        {
            Id = id, Side = side, Col = col, Row = row, Move = move,
            MaxHp = hp, CurHp = curHp, IsGeneral = general,
            ThreatValue = general ? 100 : 10,
            Stats = new BattleStats { Hp = hp, Spd = spd, PAtk = pAtk, PDef = pDef },
        };

    [Fact]
    public void Normal_Ai_Prefers_Killing_High_Value_General()
    {
        var state = new BattleState { Width = 10, Height = 6, PlayerSide = BattleSide.Defender };
        var ai = U(1, BattleSide.Attacker, 5, 3, spd: 30, move: 4, hp: 200, curHp: 200, pAtk: 100, pDef: 10);
        var weakGeneral = U(2, BattleSide.Defender, 4, 3, spd: 10, move: 4, hp: 100, curHp: 5, pAtk: 10, pDef: 10, general: true);
        var fullSoldier = U(3, BattleSide.Defender, 6, 3, spd: 10, move: 4, hp: 100, curHp: 100, pAtk: 10, pDef: 10);
        state.Units.AddRange(new[] { ai, weakGeneral, fullSoldier });

        var engine = new BattleEngine(state, new DeterministicRandom(1));
        var brain = new BattleAi(AiDifficulty.Normal);

        var turn = brain.DecideTurn(engine, ai);

        Assert.Equal(2, turn.AttackTargetId); // 选择可击杀的高价值武将
    }

    [Fact]
    public void Hard_Ai_Retreats_Low_Hp_General()
    {
        var state = new BattleState { Width = 10, Height = 6, PlayerSide = BattleSide.Defender };
        var general = U(1, BattleSide.Attacker, 5, 3, spd: 30, move: 4, hp: 100, curHp: 10, pAtk: 50, pDef: 10, general: true);
        var enemy = U(2, BattleSide.Defender, 4, 3, spd: 10, move: 4, hp: 200, curHp: 200, pAtk: 50, pDef: 10);
        state.Units.AddRange(new[] { general, enemy });

        var engine = new BattleEngine(state, new DeterministicRandom(1));
        var brain = new BattleAi(AiDifficulty.Hard);

        var turn = brain.DecideTurn(engine, general);

        Assert.NotNull(turn.MoveTo);
        Assert.Null(turn.AttackTargetId);
        Assert.True(general.IsFleeing);
        // 撤退后应远离敌人
        int distAfter = Math.Abs(turn.MoveTo!.Value.Col - enemy.Col) + Math.Abs(turn.MoveTo.Value.Row - enemy.Row);
        Assert.True(distAfter > 1);
    }

    [Fact]
    public void Ai_Controlled_Battle_Finishes()
    {
        var state = new BattleState { Width = 10, Height = 6, PlayerSide = BattleSide.Defender };
        state.Units.Add(U(1, BattleSide.Attacker, 0, 2, spd: 25, move: 4, hp: 200, curHp: 200, pAtk: 60, pDef: 20));
        state.Units.Add(U(2, BattleSide.Defender, 9, 2, spd: 20, move: 4, hp: 150, curHp: 150, pAtk: 40, pDef: 15));

        var engine = new BattleEngine(state, new DeterministicRandom(5));
        engine.SetController(BattleSide.Attacker, new BattleAi(AiDifficulty.Hard));
        engine.SetController(BattleSide.Defender, new BattleAi(AiDifficulty.Normal));
        engine.Start();

        var result = engine.FastResolveAll();
        Assert.True(result.Finished);
    }
}

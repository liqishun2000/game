using MauiApp.Game.Battle;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Tests;

public class BattleEngineTests
{
    private static BattleUnit MakeUnit(int id, BattleSide side, int col, int row, int spd, int move, int hp, int pAtk, int pDef)
        => new()
        {
            Id = id, Side = side, Col = col, Row = row, Move = move,
            MaxHp = hp, CurHp = hp,
            Stats = new BattleStats { Hp = hp, Spd = spd, PAtk = pAtk, PDef = pDef },
        };

    [Fact]
    public void Guanyu_Derived_Battle_Stats_Match_Formula()
    {
        var db = TestContent.LoadDatabase();
        var g = new GeneralInstance
        {
            TemplateId = "guanyu", Template = db.Generals["guanyu"],
            EquipmentId = "qinglongdao", Level = 1,
        };

        var s = StatCalculator.DeriveGeneralBattleStats(g, db, BalanceConfig.Default);

        Assert.Equal(1188, s.Hp);   // 200 + 95*6 + 97*4 + 30
        Assert.Equal(214, s.PAtk);  // (20 + 97*1.5 + 95*0.3) + 装备20
        Assert.Equal(37, s.Spd);    // (12 + 97*0.12 + 90*0.10)=32 + 装备5
    }

    [Fact]
    public void Unit_Derived_Stats_Include_Commander_Leadership()
    {
        var db = TestContent.LoadDatabase();
        var u = new UnitInstance { TemplateId = "default_bing", Template = db.Units["default_bing"] };

        var s = StatCalculator.DeriveUnitBattleStats(u, commanderTongshuai: 95, db, BalanceConfig.Default);

        Assert.Equal(130, s.Hp);   // 110 * (1 + 95/500=0.19)
        Assert.Equal(47, s.PAtk);  // 40 * 1.19
        Assert.Equal(40, s.PDef);  // 35 * (1 + 0.19*0.8)
        Assert.Equal(20, s.Spd);   // 速度不受统帅加成
    }

    [Fact]
    public void Move_Beyond_Range_Is_Rejected_Within_Range_Allowed()
    {
        var state = new BattleState { Width = 10, Height = 8, PlayerSide = BattleSide.Attacker };
        state.Units.Add(MakeUnit(1, BattleSide.Attacker, 0, 0, spd: 30, move: 2, hp: 100, pAtk: 10, pDef: 10));
        state.Units.Add(MakeUnit(2, BattleSide.Defender, 9, 7, spd: 10, move: 2, hp: 100, pAtk: 10, pDef: 10));

        var engine = new BattleEngine(state, new DeterministicRandom(1));
        engine.Start();

        Assert.Equal(1, engine.CurrentUnit()!.Id); // 速度高者先动
        Assert.False(engine.ExecuteTurn(UnitTurn.MoveOnly(5, 0))); // 超出移动力
        Assert.True(engine.ExecuteTurn(UnitTurn.MoveOnly(2, 0)));  // 范围内
        Assert.Equal((2, 0), (state.GetUnit(1)!.Col, state.GetUnit(1)!.Row));
    }

    [Fact]
    public void SkipToNextPlayerDecision_Stops_On_Player_Unit()
    {
        var state = new BattleState { Width = 12, Height = 8, PlayerSide = BattleSide.Attacker };
        // 防守方速度更高，先行动；两单位相距很远不会交战
        state.Units.Add(MakeUnit(1, BattleSide.Attacker, 0, 0, spd: 10, move: 2, hp: 200, pAtk: 10, pDef: 50));
        state.Units.Add(MakeUnit(2, BattleSide.Defender, 11, 7, spd: 30, move: 2, hp: 200, pAtk: 10, pDef: 50));

        var engine = new BattleEngine(state, new DeterministicRandom(1));
        engine.Start();
        Assert.Equal(BattleSide.Defender, engine.CurrentUnit()!.Side);

        engine.SkipToNextPlayerDecision();

        Assert.False(engine.IsFinished(out _));
        Assert.Equal(BattleSide.Attacker, engine.CurrentUnit()!.Side);
    }

    [Fact]
    public void Stronger_Attacker_Wins_When_FastResolved()
    {
        var s = GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", 7);
        var content = s.Content;

        var attacker = new BattleFactory.Side
        {
            FactionId = "player",
            Generals = s.Tiles["n_left1"].Generals.ToList(),
            Units = s.Tiles["n_left1"].Units.ToList(),
        };
        var defender = new BattleFactory.Side
        {
            FactionId = "rebel",
            Units = s.Tiles["n_c1_t"].Units.ToList(),
        };

        var battle = BattleFactory.CreateBattle(content, attacker, defender);
        var engine = new BattleEngine(battle, new DeterministicRandom(99));
        engine.Start();

        var result = engine.FastResolveAll();

        Assert.True(result.Finished);
        Assert.Equal(BattleOutcome.AttackerWins, result.Outcome);
        Assert.True(result.Rounds <= 30);
    }

    [Fact]
    public void Battle_Respects_30_Round_Cap()
    {
        // 双方都极肉且攻击极低，应当打满 30 回合超时
        var state = new BattleState { Width = 6, Height = 4, PlayerSide = BattleSide.Attacker };
        state.Units.Add(MakeUnit(1, BattleSide.Attacker, 0, 0, spd: 20, move: 1, hp: 10000, pAtk: 1, pDef: 9999));
        state.Units.Add(MakeUnit(2, BattleSide.Defender, 5, 3, spd: 18, move: 1, hp: 10000, pAtk: 1, pDef: 9999));

        var engine = new BattleEngine(state, new DeterministicRandom(1));
        engine.Start();
        var result = engine.FastResolveAll();

        Assert.Equal(BattleOutcome.Timeout, result.Outcome);
        Assert.Equal(30, result.Rounds);
    }
}

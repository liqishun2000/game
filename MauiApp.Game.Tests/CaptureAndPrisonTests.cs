using MauiApp.Game.Battle;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Tests;

public class CaptureAndPrisonTests
{
    private static BattleUnit Soldier(int id, BattleSide side, int col, int row, int spd, int pAtk, int hp = 200)
        => new()
        {
            Id = id, Side = side, Col = col, Row = row, Move = 4,
            MaxHp = hp, CurHp = hp,
            Stats = new BattleStats { Hp = hp, Spd = spd, PAtk = pAtk, PDef = 5 },
        };

    private static BattleUnit DownableGeneral(int id, BattleSide side, int col, int row, string templateId,
        bool bushoufu, int hp = 20, int yizhi = 50, string? equip = null, bool droppable = false)
        => new()
        {
            Id = id, Side = side, Col = col, Row = row, Move = 4,
            IsGeneral = true, GeneralTemplateId = templateId, ThreatValue = 100,
            Yizhi = yizhi, EquipmentId = equip, EquipmentDroppable = droppable,
            Traits = bushoufu ? new List<string> { "bushoufu" } : new List<string>(),
            MaxHp = hp, CurHp = hp,
            Stats = new BattleStats { Hp = hp, Spd = spd(), PDef = 1 },
        };

    private static int spd() => 5;

    [Fact]
    public void CaptureChance_Bushoufu_Is_Zero_And_Surround_Increases()
    {
        var b = BalanceConfig.Default;

        Assert.Equal(0, StatCalculator.CaptureChance(true, 4, false, 0, 100, 50, b));

        double oneSide = StatCalculator.CaptureChance(false, 1, false, 1, 50, 50, b);
        double surrounded = StatCalculator.CaptureChance(false, 4, false, 1, 50, 50, b);
        Assert.True(surrounded > oneSide);
    }

    [Fact]
    public void Bushoufu_General_Escapes_Instead_Of_Captured()
    {
        var state = new BattleState { Width = 6, Height = 4, PlayerSide = BattleSide.Attacker };
        var attacker = Soldier(1, BattleSide.Attacker, 0, 0, spd: 30, pAtk: 500);
        var general = DownableGeneral(2, BattleSide.Defender, 1, 0, "guanyu", bushoufu: true);
        state.Units.Add(attacker);
        state.Units.Add(general);

        var engine = new BattleEngine(state, new FakeRandom());
        engine.Start();
        engine.ExecuteTurn(UnitTurn.Attack(2));

        Assert.Contains("guanyu", engine.Result.EscapedGenerals);
        Assert.DoesNotContain(engine.Result.Captured, c => c.GeneralTemplateId == "guanyu");
    }

    [Fact]
    public void General_Is_Captured_And_Equipment_Drops()
    {
        var state = new BattleState { Width = 6, Height = 4, PlayerSide = BattleSide.Attacker };
        var attacker = Soldier(1, BattleSide.Attacker, 0, 0, spd: 30, pAtk: 500);
        var general = DownableGeneral(2, BattleSide.Defender, 1, 0, "ai_general_jia",
            bushoufu: false, equip: "qinglongdao", droppable: true);
        state.Units.Add(attacker);
        state.Units.Add(general);

        // FakeRandom 全 0：伤害随机=0.9，俘获 roll=0 命中，掉落 roll=0<0.5 命中
        var engine = new BattleEngine(state, new FakeRandom());
        engine.Start();
        engine.ExecuteTurn(UnitTurn.Attack(2));

        Assert.Contains(engine.Result.Captured, c => c.GeneralTemplateId == "ai_general_jia" && c.CapturedBy == BattleSide.Attacker);
        Assert.Contains(engine.Result.Drops, d => d.EquipmentId == "qinglongdao" && d.ToSide == BattleSide.Attacker);
    }

    [Fact]
    public void Persuade_Succeeds_And_Moves_General_To_Faction()
    {
        var s = GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", 1);
        var player = s.Factions["player"];
        var prisoner = new GeneralInstance
        {
            TemplateId = "ai_general_jia", Template = s.Content.Generals["ai_general_jia"],
            FactionId = "ai_lord", Status = GeneralStatus.Captured, DetainedMonths = 3,
        };
        player.Prison.Add(prisoner);

        var prison = new PrisonService(s, new FakeRandom(0.0)); // roll 0 -> 成功
        var r = prison.Persuade("player", "ai_general_jia");

        Assert.True(r.Success, r.Message);
        Assert.Empty(player.Prison);
        Assert.Equal(GeneralStatus.Active, prisoner.Status);
        Assert.Equal("player", prisoner.FactionId);
        Assert.Contains(s.Tiles["n_left1"].Generals, g => g.TemplateId == "ai_general_jia");
    }

    [Fact]
    public void Persuade_Fails_Then_Prisoner_May_Escape()
    {
        var s = GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", 1);
        var player = s.Factions["player"];
        var prisoner = new GeneralInstance
        {
            TemplateId = "ai_general_jia", Template = s.Content.Generals["ai_general_jia"],
            FactionId = "ai_lord", Status = GeneralStatus.Captured, DetainedMonths = 3,
        };
        player.Prison.Add(prisoner);

        // 第一次 roll 0.99 -> 招降失败；第二次 roll 0 -> 越狱成功
        var prison = new PrisonService(s, new FakeRandom(0.99, 0.0));
        var r = prison.Persuade("player", "ai_general_jia");

        Assert.False(r.Success);
        Assert.Empty(player.Prison);
        Assert.Equal(GeneralStatus.Escaped, prisoner.Status);
    }
}

using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Tests;

public class WorldEngineTests
{
    private static GameState NewGame(int seed = 12345) =>
        GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", seed);

    private static WorldEngine Engine(GameState state, int seed = 1) =>
        new(state, new DeterministicRandom(seed));

    [Fact]
    public void NewGame_Initializes_Factions_Tiles_And_Adjacency()
    {
        var s = NewGame();

        Assert.Equal(3, s.Factions.Count);
        Assert.Equal(9, s.Tiles.Count);

        var left = s.Tiles["n_left1"];
        Assert.Equal("player", left.OwnerFactionId);
        Assert.Equal(2, left.Generals.Count);
        Assert.Equal(5, left.Units.Count);
        Assert.Contains("n_c1_t", left.Adjacent);
        Assert.Contains("n_c1_b", left.Adjacent);

        Assert.Equal(500, s.Factions["player"].Gold);
    }

    [Fact]
    public void AdvanceMonth_Produces_Food_From_Farm_Minus_Upkeep()
    {
        var s = NewGame();
        var engine = Engine(s);

        // n_left1: 农田 80 *(1 + 80/250=0.32) = floor(105.6)=105；驻军 5 乡勇 upkeep 10
        var report = engine.AdvanceMonth();
        var player = s.Factions["player"];

        var summary = report.Factions.Single(f => f.FactionId == "player");
        Assert.Equal(105, summary.FoodProduced);
        Assert.Equal(10, summary.FoodUpkeep);
        Assert.Equal(0, summary.GoldGained);
        Assert.Equal(1000 + 105 - 10, player.Food);
        Assert.Equal(2, s.Month);
    }

    [Fact]
    public void Recruit_Adds_Units_And_Deducts_Resources()
    {
        var s = NewGame();
        var engine = Engine(s);

        // 主将魅力 95 -> 折扣 min(95/400=0.2375,0.25)=0.2375
        // 单价 gold ceil(50*0.7625)=39, food ceil(30*0.7625)=23；×3 = gold117 food69
        var r = engine.Recruit("player", "n_left1", "default_bing", 3);

        Assert.True(r.Success, r.Message);
        Assert.Equal(8, s.Tiles["n_left1"].Units.Count);
        Assert.Equal(500 - 117, s.Factions["player"].Gold);
        Assert.Equal(1000 - 69, s.Factions["player"].Food);
    }

    [Fact]
    public void Recruit_Special_Unit_Beyond_MaxCount_Fails()
    {
        var s = NewGame();
        var engine = Engine(s);

        var r = engine.Recruit("ai_lord", "n_right1", "qingzhou_bing", 31);

        Assert.False(r.Success);
        Assert.Contains("上限", r.Message);
    }

    [Fact]
    public void Recruit_On_Rebel_Or_Foreign_Tile_Fails()
    {
        var s = NewGame();
        var engine = Engine(s);

        Assert.False(engine.Recruit("player", "n_mid_m", "default_bing", 1).Success); // 叛军地盘
        Assert.False(engine.Recruit("player", "n_right1", "default_bing", 1).Success); // 敌方地盘
    }

    [Fact]
    public void Build_Then_Complete_And_Produce_Next_Month()
    {
        var s = NewGame();
        var engine = Engine(s);

        var build = engine.Build("player", "n_left1", "market");
        Assert.True(build.Success, build.Message);

        var r1 = engine.AdvanceMonth(); // 集市本月完工，尚未产出
        Assert.Contains(r1.CompletedBuildings, c => c.Contains("集市"));
        Assert.Equal(0, r1.Factions.Single(f => f.FactionId == "player").GoldGained);

        var r2 = engine.AdvanceMonth(); // 集市 60 *(1.32) = floor(79.2)=79
        Assert.Equal(79, r2.Factions.Single(f => f.FactionId == "player").GoldGained);
    }

    [Fact]
    public void Starvation_Causes_Desertion_And_Zeroes_Food()
    {
        var s = NewGame();
        var engine = Engine(s);

        var player = s.Factions["player"];
        player.Gold = 1_000_000;
        player.Food = 1_000_000;
        var rec = engine.Recruit("player", "n_left1", "default_bing", 100);
        Assert.True(rec.Success, rec.Message);

        int before = s.Tiles["n_left1"].Units.Count; // 5 + 100
        Assert.Equal(105, before);

        player.Food = 0; // 招募后清零粮食，制造断粮
        var report = engine.AdvanceMonth();
        var summary = report.Factions.Single(f => f.FactionId == "player");

        Assert.True(summary.Deserters > 0, "应触发逃兵");
        Assert.Equal(0, player.Food);
        Assert.True(s.Tiles["n_left1"].Units.Count < before, "逃兵后单位减少");
    }

    [Fact]
    public void Rebel_Faction_Is_Static_No_Production_Or_Upkeep()
    {
        var s = NewGame();
        var engine = Engine(s);

        int rebelUnitsBefore = s.TilesOf("rebel").SelectMany(t => t.Units).Count();
        var report = engine.AdvanceMonth();

        Assert.DoesNotContain(report.Factions, f => f.FactionId == "rebel");
        int rebelUnitsAfter = s.TilesOf("rebel").SelectMany(t => t.Units).Count();
        Assert.Equal(rebelUnitsBefore, rebelUnitsAfter);
    }
}

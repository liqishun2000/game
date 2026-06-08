using MauiApp.Game.App;
using MauiApp.Game.Battle;
using MauiApp.Game.Stats;
using MauiApp.Game.World;

namespace MauiApp.Game.Tests;

public class ExpeditionPlannerTests
{
    [Fact]
    public void Validate_Requires_At_Least_One_General()
    {
        var s = GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", 1);
        var tile = s.Tiles["n_left1"];
        var setup = new ExpeditionSetup
        {
            GeneralTemplateIds = Array.Empty<string>(),
            UnitWorldIds = tile.Units.Select(u => u.Id).ToList(),
            CarriedFood = 0,
        };

        var r = ExpeditionPlanner.Validate(s, tile.Id, setup);
        Assert.False(r.Success);
    }

    [Fact]
    public void StartPlayerAttack_Deducts_Food_And_Marks_Generals()
    {
        var db = TestContent.LoadDatabase();
        var session = GameSession.Start(db, "v1_countryside", 7);
        var tile = session.State.Tiles["n_left1"];
        int foodBefore = session.PlayerFaction.Food;
        var guanyu = tile.Generals.First(g => g.TemplateId == "guanyu");

        var setup = ExpeditionSetup.AllFromTile(tile, carriedFood: 50);
        var pending = session.StartPlayerAttack("n_left1", "n_c1_t", setup);

        Assert.Equal(foodBefore - 50, session.PlayerFaction.Food);
        Assert.True(guanyu.ActedThisMonth);
        Assert.Equal(50, pending.Engine.State.SideFood[BattleSide.Attacker]);
        Assert.True(pending.AwaitDeployment);
        Assert.False(pending.Engine.State.IsStarted);
    }

    [Fact]
    public void Battle_Starvation_Lowers_Morale()
    {
        var s = GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", 3);
        var atk = s.Tiles["n_left1"];
        var target = atk.Adjacent.Select(id => s.Tiles[id]).First(t => t.IsRebelFixed);

        var war = new WarService(s, new DeterministicRandom(3));
        var pending = war.CreateBattle(
            atk.Id, target.Id,
            atk.Generals.Take(1).Select(g => g.TemplateId),
            Array.Empty<int>(),
            attackerFood: 0);

        pending.Engine.Start();
        int moraleBefore = pending.Engine.State.AliveOf(BattleSide.Attacker).First().Morale;

        // 快速推进到回合末触发断粮
        pending.Engine.FastResolveTurn();

        int moraleAfter = pending.Engine.State.AliveOf(BattleSide.Attacker).First().Morale;
        Assert.True(moraleAfter < moraleBefore);
    }
}

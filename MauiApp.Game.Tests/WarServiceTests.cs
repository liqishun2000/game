using MauiApp.Game.Battle;
using MauiApp.Game.Stats;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Tests;

public class WarServiceTests
{
    [Fact]
    public void Player_Conquers_Adjacent_Rebel_Tile_On_Victory()
    {
        var s = GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", 7);

        var atk = s.Tiles["n_left1"];
        var target = atk.Adjacent
            .Select(id => s.Tiles[id])
            .First(t => t.IsRebelFixed);

        Assert.NotEqual("player", target.OwnerFactionId);
        int defenderUnits = target.Units.Count;
        Assert.True(defenderUnits > 0);

        var war = new WarService(s, new DeterministicRandom(7));
        var pending = war.CreateBattle(
            atk.Id, target.Id,
            atk.Generals.Select(g => g.TemplateId),
            atk.Units.Select(u => u.Id));

        pending.Engine.Start();
        var result = pending.Engine.FastResolveAll();
        Assert.Equal(BattleOutcome.AttackerWins, result.Outcome);

        war.ApplyResult(pending);

        // 占领：易主且不再是固定反贼
        Assert.Equal("player", target.OwnerFactionId);
        Assert.False(target.IsRebelFixed);
        // 进攻方武将推进到占领地
        Assert.Contains(target.Generals, g => g.TemplateId == "liubei");
        // 防守方残兵清零
        Assert.DoesNotContain(target.Units, u => u.FactionId == "rebel");
    }

    [Fact]
    public void CreateBattle_Rejects_NonAdjacent_Target()
    {
        var s = GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", 1);
        var atk = s.Tiles["n_left1"];
        var far = s.Tiles["n_right1"];

        var war = new WarService(s, new DeterministicRandom(1));

        Assert.Throws<InvalidOperationException>(() =>
            war.CreateBattle(atk.Id, far.Id, atk.Generals.Select(g => g.TemplateId), atk.Units.Select(u => u.Id)));
    }
}

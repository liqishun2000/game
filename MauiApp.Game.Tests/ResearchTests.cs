using MauiApp.Game.Stats;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Tests;

public class ResearchTests
{
    private static (GameState State, WorldEngine Engine) NewWorld()
    {
        var s = GameStateFactory.CreateNewGame(TestContent.LoadDatabase(), "v1_countryside", 1);
        return (s, new WorldEngine(s, new DeterministicRandom(1)));
    }

    [Fact]
    public void Research_Requires_Resources_And_Records_Tech()
    {
        var (s, engine) = NewWorld();
        var techId = s.Content.Techs.Keys.First(id => s.Content.Techs[id].PrereqIds.Count == 0);
        var tech = s.Content.Techs[techId];

        var player = s.Factions["player"];
        player.Gold = tech.Cost.Gold;
        player.TechPoints = tech.Cost.TechPoints;
        player.Food = tech.Cost.Food;

        var r = engine.Research("player", techId);

        Assert.True(r.Success, r.Message);
        Assert.Contains(techId, player.ResearchedTechIds);
        // 不能重复研究
        Assert.False(engine.Research("player", techId).Success);
    }

    [Fact]
    public void Research_Blocked_By_Missing_Prerequisite()
    {
        var (s, engine) = NewWorld();
        var techWithPrereq = s.Content.Techs.Values.FirstOrDefault(t => t.PrereqIds.Count > 0);
        if (techWithPrereq is null) return; // v1 若无前置科技则跳过

        var player = s.Factions["player"];
        player.Gold = 99999;
        player.TechPoints = 99999;

        var r = engine.Research("player", techWithPrereq.Id);
        Assert.False(r.Success);
    }
}

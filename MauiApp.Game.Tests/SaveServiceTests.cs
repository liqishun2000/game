using MauiApp.Game.Save;
using MauiApp.Game.Stats;
using MauiApp.Game.World;

namespace MauiApp.Game.Tests;

public class SaveServiceTests
{
    [Fact]
    public void Save_Then_Load_Roundtrips_Core_State()
    {
        var db = TestContent.LoadDatabase();
        var s = GameStateFactory.CreateNewGame(db, "v1_countryside", 42);

        // 制造一些可变状态：推进、招兵、改资源
        var world = new WorldEngine(s, new DeterministicRandom(42));
        s.Factions["player"].Gold = 9999;
        world.Recruit("player", "n_left1", "default_bing", 3);
        world.AdvanceMonth();

        int beforeUnits = s.Tiles["n_left1"].Units.Count;
        int beforeMonth = s.Month;
        int beforeGold = s.Factions["player"].Gold;

        string json = SaveService.Serialize(s);
        var loaded = SaveService.Deserialize(json, db);

        Assert.Equal(beforeMonth, loaded.Month);
        Assert.Equal(s.NextUnitId, loaded.NextUnitId);
        Assert.Equal(beforeGold, loaded.Factions["player"].Gold);
        Assert.Equal(beforeUnits, loaded.Tiles["n_left1"].Units.Count);
        // 结构重建：相邻关系仍在
        Assert.Equal(s.Tiles["n_left1"].Adjacent.Count, loaded.Tiles["n_left1"].Adjacent.Count);
        // 模板已重新绑定
        Assert.All(loaded.Tiles["n_left1"].Units, u => Assert.NotNull(u.Template));
    }
}

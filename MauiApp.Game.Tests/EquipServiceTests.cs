using MauiApp.Game.App;
using MauiApp.Game.Content;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Tests;

public class EquipServiceTests
{
    private static ContentDatabase LoadDb()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var result = new ContentLoader().LoadFromDirectory(root);
        Assert.True(result.Success, result.Validation.ToString());
        return result.Database!;
    }

    [Fact]
    public void Unequip_Returns_Unique_To_Armory()
    {
        var db = LoadDb();
        var session = GameSession.Start(db, "v1_countryside", 1);
        var equip = session.Equip;

        var r = equip.Unequip("player", "liubei");
        Assert.True(r.Success);
        Assert.Contains("shuanggujian", session.PlayerFaction.Armory);
        Assert.Null(session.State.Tiles["n_left1"].Generals.First(g => g.TemplateId == "liubei").EquipmentId);
    }

    [Fact]
    public void Equip_From_Armory_And_Swap_Between_Generals()
    {
        var db = LoadDb();
        var session = GameSession.Start(db, "v1_countryside", 2);
        var equip = session.Equip;
        var faction = session.PlayerFaction;

        equip.Unequip("player", "liubei");
        equip.Unequip("player", "guanyu");

        Assert.Contains("shuanggujian", faction.Armory);
        Assert.Contains("qinglongdao", faction.Armory);

        Assert.True(equip.Equip("player", "liubei", "qinglongdao").Success);
        Assert.Equal("qinglongdao", session.State.Tiles["n_left1"].Generals.First(g => g.TemplateId == "liubei").EquipmentId);
        Assert.DoesNotContain("qinglongdao", faction.Armory);

        Assert.True(equip.Equip("player", "guanyu", "shuanggujian").Success);
        Assert.True(equip.Equip("player", "liubei", "shuanggujian").Success);

        var liubei = session.State.Tiles["n_left1"].Generals.First(g => g.TemplateId == "liubei");
        var guanyu = session.State.Tiles["n_left1"].Generals.First(g => g.TemplateId == "guanyu");
        Assert.Equal("shuanggujian", liubei.EquipmentId);
        Assert.Null(guanyu.EquipmentId);
    }

    [Fact]
    public void Cannot_Equip_Unit_Gear_On_General()
    {
        var db = LoadDb();
        var session = GameSession.Start(db, "v1_countryside", 3);
        session.PlayerFaction.Armory.Add("tie_jia");
        session.PlayerFaction.ResearchedTechIds.Add("forging_1");

        var r = session.Equip.Equip("player", "liubei", "tie_jia");
        Assert.False(r.Success);
    }
}

using MauiApp.Game.Content;

namespace MauiApp.Game.Tests;

public class ContentLoaderTests
{
    private static ContentLoadResult LoadV1()
    {
        // content/ 由 csproj 从 MauiApp/Resources/Raw 拷贝而来
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        return new ContentLoader().LoadFromDirectory(root);
    }

    [Fact]
    public void V1Content_Loads_Without_Errors()
    {
        var result = LoadV1();
        Assert.True(result.Success, result.Validation.ToString());
    }

    [Fact]
    public void V1Content_Has_Expected_Counts()
    {
        var db = LoadV1().Database;

        Assert.Equal(3, db.Factions.Count);
        Assert.Equal(5, db.Generals.Count);
        Assert.Equal(3, db.Units.Count);
        Assert.Equal(4, db.Equipment.Count);
        Assert.Equal(5, db.Buildings.Count);
        Assert.Equal(2, db.Techs.Count);
        Assert.Single(db.Maps);
    }

    [Fact]
    public void Map_Has_Nine_Nodes_And_Connected_Spawns()
    {
        var db = LoadV1().Database;
        var map = db.Maps["v1_countryside"];

        Assert.Equal(new[] { 1, 2, 3, 2, 1 }, map.ColumnLayout);
        Assert.Equal(9, map.Nodes.Count);
        Assert.Equal("n_left1", map.Spawns["player"]);
        Assert.Equal("n_right1", map.Spawns["ai"]);
    }

    [Fact]
    public void Special_Units_Have_Positive_MaxCount()
    {
        var db = LoadV1().Database;
        foreach (var u in db.Units.Values)
        {
            if (u.IsSpecial)
                Assert.True(u.MaxCount is > 0, $"{u.Id} 应有正的 maxCount");
        }
    }

    [Fact]
    public void Guanyu_Carries_Unique_General_Only_Equipment()
    {
        var db = LoadV1().Database;
        var guanyu = db.Generals["guanyu"];
        Assert.Equal("qinglongdao", guanyu.DefaultEquipmentId);

        var blade = db.Equipment[guanyu.DefaultEquipmentId!];
        Assert.True(blade.IsUnique);
        Assert.True(blade.ForGeneralOnly);
        Assert.Contains("bushoufu", guanyu.Traits);
    }

    [Fact]
    public void Missing_Reference_Is_Reported_As_Error()
    {
        var sources = new ContentJsonSources
        {
            Generals = "[{\"id\":\"x\",\"name\":\"X\",\"defaultEquipmentId\":\"does_not_exist\"}]",
        };

        var result = new ContentLoader().Load(sources);
        Assert.False(result.Success);
        Assert.Contains(result.Validation.Errors, e => e.Contains("does_not_exist"));
    }
}

using MauiApp.Game.Content;

namespace MauiApp.Game.Tests;

internal static class TestContent
{
    public static ContentDatabase LoadDatabase()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var result = new ContentLoader().LoadFromDirectory(root);
        Assert.True(result.Success, result.Validation.ToString());
        return result.Database;
    }
}

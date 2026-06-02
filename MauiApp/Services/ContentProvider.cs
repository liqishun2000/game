using MauiApp.Game.Content;

namespace MauiApp.Services;

/// <summary>从 MAUI 应用包内的 Raw 资源读取游戏内容 JSON 并加载为内容数据库。</summary>
public static class ContentProvider
{
    private static readonly string[] MapFiles = { "maps/v1_countryside.json" };

    public static async Task<ContentLoadResult> LoadAsync()
    {
        var sources = new ContentJsonSources
        {
            Factions = await ReadAsync("data/factions.json"),
            Generals = await ReadAsync("data/generals.json"),
            Units = await ReadAsync("data/units.json"),
            Equipment = await ReadAsync("data/equipment.json"),
            Buildings = await ReadAsync("data/buildings.json"),
            Techs = await ReadAsync("data/tech.json"),
        };

        foreach (var map in MapFiles)
            sources.Maps.Add(await ReadAsync(map));

        return new ContentLoader().Load(sources);
    }

    private static async Task<string> ReadAsync(string logicalPath)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(logicalPath);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}

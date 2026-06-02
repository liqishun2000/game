using MauiApp.Game.Content;

namespace MauiApp.Services;

/// <summary>
/// 读取游戏内容 JSON：内置数据来自应用包 Raw 资源；
/// 此外扫描应用数据目录下的 maps/ 以加载玩家自定义地图（M9 扩展开口）。
/// </summary>
public static class ContentProvider
{
    private static readonly string[] BuiltInMapFiles = { "maps/v1_countryside.json" };

    /// <summary>用户自定义地图目录：把额外 *.json 地图放到这里即可被加载。</summary>
    public static string UserMapsDirectory => Path.Combine(FileSystem.AppDataDirectory, "maps");

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

        foreach (var map in BuiltInMapFiles)
            sources.Maps.Add(await ReadAsync(map));

        // 用户自定义地图（可选）
        if (Directory.Exists(UserMapsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(UserMapsDirectory, "*.json").OrderBy(f => f))
                sources.Maps.Add(await File.ReadAllTextAsync(file));
        }

        return new ContentLoader().Load(sources);
    }

    private static async Task<string> ReadAsync(string logicalPath)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(logicalPath);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}

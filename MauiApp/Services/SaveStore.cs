using MauiApp.Game.App;
using MauiApp.Game.Save;
using MauiApp.Game.Stats;

namespace MauiApp.Services;

/// <summary>把存档读写到应用数据目录（单槽位 v1）。</summary>
public static class SaveStore
{
    private static string Path => System.IO.Path.Combine(FileSystem.AppDataDirectory, "save_v1.json");

    public static bool Exists => File.Exists(Path);

    public static async Task SaveAsync(GameSession session) =>
        await File.WriteAllTextAsync(Path, SaveService.Serialize(session.State));

    public static async Task<GameSession> LoadAsync()
    {
        var content = await ContentProvider.LoadAsync();
        if (!content.Success)
            throw new InvalidOperationException("内容校验失败，无法读取存档");

        string json = await File.ReadAllTextAsync(Path);
        var state = SaveService.Deserialize(json, content.Database);
        return new GameSession(state, new DeterministicRandom(state.Seed + state.Month), state.Difficulty);
    }
}

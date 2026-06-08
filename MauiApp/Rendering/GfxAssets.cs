using MauiApp.Services;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>全局像素贴图缓存与 UI 头像源。</summary>
public static class GfxAssets
{
    public static AssetCache Cache { get; } = new();

    private static readonly Dictionary<string, ImageSource> UiPortraits = new();
    private static bool _battleCoreLoaded;

    public static async Task EnsureBattleCoreAsync()
    {
        if (_battleCoreLoaded) return;
        await Cache.PreloadAsync(GfxKeys.BattleTiles);
        _battleCoreLoaded = true;
    }

    public static async Task PreloadPortraitsAsync(IEnumerable<string> generalIds)
    {
        var paths = generalIds.Select(GfxKeys.Portrait).Distinct().ToList();
        await Cache.PreloadAsync(paths);
    }

    public static async Task<ImageSource?> GetUiPortraitAsync(string generalTemplateId)
    {
        if (UiPortraits.TryGetValue(generalTemplateId, out var cached))
            return cached;

        var img = await Cache.LoadAsync(GfxKeys.Portrait(generalTemplateId));
        if (img is null) return null;

        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        if (data is null) return null;

        var bytes = data.ToArray();
        var source = ImageSource.FromStream(() => new MemoryStream(bytes));
        UiPortraits[generalTemplateId] = source;
        return source;
    }
}

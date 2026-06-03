using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>
/// SkiaSharp 用的像素中文字体（Zpix）。从 Raw 资源 <c>fonts/zpix.ttf</c> 加载并缓存；
/// 加载失败回退系统默认字体。页面首帧前调用 <see cref="EnsureLoadedAsync"/> 预热。
/// </summary>
public static class PixelFont
{
    private static SKTypeface? _tf;
    private static bool _tried;

    public static SKTypeface Typeface => _tf ?? SKTypeface.Default;

    public static async Task EnsureLoadedAsync()
    {
        if (_tried) return;
        _tried = true;
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("fonts/zpix.ttf");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            _tf = SKTypeface.FromStream(ms);
        }
        catch
        {
            _tf = null;
        }
    }
}

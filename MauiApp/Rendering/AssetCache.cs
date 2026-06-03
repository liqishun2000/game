using System.Collections.Concurrent;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>
/// 像素贴图缓存：从应用包 <c>Resources/Raw/art/</c>（MauiAsset）解码 PNG 为 <see cref="SKImage"/> 并按 key 缓存，
/// 避免每帧解码。渲染时统一使用 <see cref="Nearest"/> 采样保证像素清晰（见规划 4.1）。
/// </summary>
public sealed class AssetCache
{
    /// <summary>最近邻采样：像素图放大不糊的关键。</summary>
    public static readonly SKSamplingOptions Nearest = new(SKFilterMode.Nearest, SKMipmapMode.None);

    private readonly ConcurrentDictionary<string, SKImage?> _cache = new();

    /// <summary>是否已缓存（含已知缺失）。</summary>
    public bool Has(string key) => _cache.ContainsKey(key);

    /// <summary>取已缓存图像；未加载或缺失返回 null（调用方应回退到几何画法）。</summary>
    public SKImage? Get(string key) => _cache.TryGetValue(key, out var img) ? img : null;

    /// <summary>
    /// 预加载一组贴图（logicalPath 形如 "art/tiles/grass.png"）。
    /// 缺失文件记为 null 不抛异常，渲染层据此回退。
    /// </summary>
    public async Task PreloadAsync(IEnumerable<string> logicalPaths)
    {
        foreach (var path in logicalPaths)
        {
            if (_cache.ContainsKey(path)) continue;
            _cache[path] = await TryDecodeAsync(path);
        }
    }

    /// <summary>按需加载单张（已缓存直接返回）。</summary>
    public async Task<SKImage?> LoadAsync(string logicalPath)
    {
        if (_cache.TryGetValue(logicalPath, out var cached)) return cached;
        var img = await TryDecodeAsync(logicalPath);
        _cache[logicalPath] = img;
        return img;
    }

    /// <summary>注入已生成的图像（如运行期程序化占位图），便于统一通过 Get 取用。</summary>
    public void Put(string key, SKImage image) => _cache[key] = image;

    private static async Task<SKImage?> TryDecodeAsync(string logicalPath)
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(logicalPath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            using var data = SKData.Create(ms);
            return data is null ? null : SKImage.FromEncodedData(data);
        }
        catch
        {
            return null;
        }
    }
}

using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>
/// 战斗飘字（伤害/暴击/被俘等）：从起点上浮并淡出。
/// 坐标用**格坐标**（X=列向，Y=行向，与单位同一空间），由渲染器转设备像素，保持分辨率无关。
/// </summary>
public sealed class FloatingText
{
    public required string Text { get; init; }
    public SKColor Color { get; init; } = SKColors.White;
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>字号占格高比例（渲染器 = CellSize * SizeFactor）。</summary>
    public float SizeFactor { get; init; } = 0.34f;
    public float Age { get; set; }
    public float Life { get; init; } = 0.85f;

    /// <summary>上浮速度（格/秒）。</summary>
    public float RiseSpeed { get; init; } = 1.1f;

    public bool Done => Age >= Life;
    public float Alpha => Math.Clamp(1f - Age / Life, 0f, 1f);

    /// <summary>推进；返回是否仍存活。</summary>
    public bool Advance(float dt)
    {
        Age += dt;
        Y -= RiseSpeed * dt;
        return !Done;
    }
}

using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>
/// 图集切片助手：把一张网格排布的精灵图集按 cellWidth×cellHeight 切成子图。
/// 子图 <see cref="SKImage"/> 按 (col,row) 缓存，渲染用最近邻采样绘制。
/// </summary>
public sealed class PixelAtlas
{
    private readonly SKImage _sheet;
    private readonly int _cellW;
    private readonly int _cellH;
    private readonly Dictionary<(int, int), SKImage> _slices = new();

    public PixelAtlas(SKImage sheet, int cellWidth, int cellHeight)
    {
        _sheet = sheet;
        _cellW = cellWidth;
        _cellH = cellHeight;
        Columns = sheet.Width / cellWidth;
        Rows = sheet.Height / cellHeight;
    }

    public int Columns { get; }
    public int Rows { get; }

    /// <summary>取第 (col,row) 个格子的子图（越界返回 null）。</summary>
    public SKImage? Slice(int col, int row)
    {
        if (col < 0 || row < 0 || col >= Columns || row >= Rows) return null;
        if (_slices.TryGetValue((col, row), out var cached)) return cached;

        var sub = _sheet.Subset(new SKRectI(col * _cellW, row * _cellH, (col + 1) * _cellW, (row + 1) * _cellH));
        _slices[(col, row)] = sub;
        return sub;
    }

    /// <summary>按线性索引取子图（从左到右、从上到下）。</summary>
    public SKImage? Slice(int index) => Columns == 0 ? null : Slice(index % Columns, index / Columns);
}

using MauiApp.Game.World.State;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>把地盘节点按列布局映射到世界坐标，供绘制、相机与点击命中复用。</summary>
public sealed class MapLayout
{
    public Dictionary<string, SKPoint> Positions { get; } = new();
    public float NodeRadius { get; private set; }
    public float ContentWidth { get; private set; }
    public float ContentHeight { get; private set; }
    /// <summary>节点实际占用区域（含半径），用于初始相机对准。</summary>
    public SKRect NodeBounds { get; private set; }

    private const float ColSpacing = 220f;
    private const float RowSpacing = 170f;
    private const float Margin = 100f;

    public static MapLayout Build(GameState state, float viewportW, float viewportH)
    {
        var layout = new MapLayout();
        var tiles = state.Tiles.Values.ToList();
        if (tiles.Count == 0) return layout;

        var cols = tiles.Select(t => t.Col).Distinct().OrderBy(c => c).ToList();
        int maxRow = tiles.Max(t => t.Row);

        layout.NodeRadius = 36f;
        layout.ContentWidth = Margin * 2 + Math.Max(0, cols.Count - 1) * ColSpacing;
        layout.ContentHeight = Margin * 2 + Math.Max(0, maxRow) * RowSpacing;

        for (int ci = 0; ci < cols.Count; ci++)
        {
            float x = Margin + ci * ColSpacing;
            var colTiles = tiles.Where(t => t.Col == cols[ci]).OrderBy(t => t.Row).ToList();
            for (int ri = 0; ri < colTiles.Count; ri++)
            {
                float y = Margin + ri * RowSpacing;
                layout.Positions[colTiles[ri].Id] = new SKPoint(x, y);
            }
        }

        float r = layout.NodeRadius;
        float minX = layout.Positions.Values.Min(p => p.X) - r;
        float maxX = layout.Positions.Values.Max(p => p.X) + r;
        float minY = layout.Positions.Values.Min(p => p.Y) - r;
        float maxY = layout.Positions.Values.Max(p => p.Y) + r;
        layout.NodeBounds = SKRect.Create(minX, minY, maxX - minX, maxY - minY);

        // 内容区覆盖全部节点，并至少与视口同大以便拖拽
        float pad = 60f;
        layout.ContentWidth = Math.Max(maxX + pad, viewportW);
        layout.ContentHeight = Math.Max(maxY + pad, viewportH);

        return layout;
    }

    public string? HitTest(float worldX, float worldY)
    {
        foreach (var (id, p) in Positions)
        {
            float dx = p.X - worldX, dy = p.Y - worldY;
            if (dx * dx + dy * dy <= NodeRadius * NodeRadius * 1.6f)
                return id;
        }

        return null;
    }
}

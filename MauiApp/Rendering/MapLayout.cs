using MauiApp.Game.World.State;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>把地盘节点按列布局映射到画布坐标，供绘制与点击命中复用。</summary>
public sealed class MapLayout
{
    public Dictionary<string, SKPoint> Positions { get; } = new();
    public float NodeRadius { get; private set; }

    public static MapLayout Build(GameState state, float width, float height)
    {
        var layout = new MapLayout();
        var tiles = state.Tiles.Values.ToList();
        if (tiles.Count == 0) return layout;

        var cols = tiles.Select(t => t.Col).Distinct().OrderBy(c => c).ToList();
        float marginX = width * 0.10f;
        float marginY = height * 0.12f;
        float usableW = Math.Max(1, width - marginX * 2);
        float usableH = Math.Max(1, height - marginY * 2);

        layout.NodeRadius = Math.Clamp(Math.Min(usableW, usableH) * 0.06f, 16f, 40f);

        for (int ci = 0; ci < cols.Count; ci++)
        {
            float x = cols.Count == 1 ? width / 2 : marginX + usableW * ci / (cols.Count - 1);
            var colTiles = tiles.Where(t => t.Col == cols[ci]).OrderBy(t => t.Row).ToList();
            for (int ri = 0; ri < colTiles.Count; ri++)
            {
                float y = colTiles.Count == 1
                    ? height / 2
                    : marginY + usableH * ri / (colTiles.Count - 1);
                layout.Positions[colTiles[ri].Id] = new SKPoint(x, y);
            }
        }

        return layout;
    }

    public string? HitTest(float x, float y)
    {
        foreach (var (id, p) in Positions)
        {
            float dx = p.X - x, dy = p.Y - y;
            if (dx * dx + dy * dy <= NodeRadius * NodeRadius * 1.6f)
                return id;
        }

        return null;
    }
}

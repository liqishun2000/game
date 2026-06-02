using MauiApp.Game.World.State;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>大地图绘制：道路、地盘节点、归属配色、选中与可攻击高亮。</summary>
public sealed class WorldMapRenderer
{
    public void Draw(SKCanvas canvas, SKImageInfo info, GameState state, MapLayout layout,
        string? selectedTileId, IReadOnlySet<string> attackTargets)
    {
        canvas.Clear(new SKColor(0x1c, 0x20, 0x28));
        if (layout.Positions.Count == 0) return;

        DrawRoads(canvas, state, layout);
        DrawNodes(canvas, state, layout, selectedTileId, attackTargets);
    }

    private static void DrawRoads(SKCanvas canvas, GameState state, MapLayout layout)
    {
        using var road = new SKPaint
        {
            Color = new SKColor(0x55, 0x5c, 0x66), StrokeWidth = 4, IsAntialias = true, Style = SKPaintStyle.Stroke,
        };

        var drawn = new HashSet<string>();
        foreach (var tile in state.Tiles.Values)
        {
            if (!layout.Positions.TryGetValue(tile.Id, out var a)) continue;
            foreach (var adj in tile.Adjacent)
            {
                var key = string.CompareOrdinal(tile.Id, adj) < 0 ? $"{tile.Id}|{adj}" : $"{adj}|{tile.Id}";
                if (!drawn.Add(key)) continue;
                if (layout.Positions.TryGetValue(adj, out var b))
                    canvas.DrawLine(a, b, road);
            }
        }
    }

    private static void DrawNodes(SKCanvas canvas, GameState state, MapLayout layout,
        string? selectedTileId, IReadOnlySet<string> attackTargets)
    {
        float r = layout.NodeRadius;
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
        using var text = new SKPaint { IsAntialias = true, Color = SKColors.White };
        using var sub = new SKPaint { IsAntialias = true, Color = new SKColor(0xe0, 0xe0, 0xe0) };
        using var titleFont = new SKFont { Size = 18 };
        using var subFont = new SKFont { Size = 14 };

        foreach (var tile in state.Tiles.Values)
        {
            if (!layout.Positions.TryGetValue(tile.Id, out var p)) continue;

            fill.Color = FactionColors.For(state, tile);
            canvas.DrawCircle(p, r, fill);

            if (tile.Id == selectedTileId)
            {
                ring.Color = SKColors.White;
                canvas.DrawCircle(p, r + 5, ring);
            }
            else if (attackTargets.Contains(tile.Id))
            {
                ring.Color = new SKColor(0xff, 0xb0, 0x3a);
                canvas.DrawCircle(p, r + 5, ring);
            }

            canvas.DrawText(tile.Name, p.X, p.Y - r - 8, SKTextAlign.Center, titleFont, text);
            canvas.DrawText($"将{tile.Generals.Count} 兵{tile.Units.Count}", p.X, p.Y + r + 20, SKTextAlign.Center, subFont, sub);
        }
    }
}

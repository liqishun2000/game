using MauiApp.Game.Model;
using MauiApp.Game.World.State;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>
/// 大地图像素绘制：羊皮纸底图 + 像素土路 + 按地块类型（城池/关隘/村庄）绘制像素建筑 +
/// 阵营旗帜/底色 + 选中/可攻击呼吸高亮 + 反馈飘字。
/// </summary>
public sealed class WorldMapRenderer
{
    public void Draw(SKCanvas canvas, SKImageInfo info, GameState state, MapLayout layout,
        string? selectedTileId, IReadOnlySet<string> attackTargets, float time,
        MapCamera camera, IReadOnlyList<FloatingText>? feedback = null)
    {
        DrawParchment(canvas, info);
        if (layout.Positions.Count == 0) return;

        camera.Clamp(info.Width, info.Height, layout.ContentWidth, layout.ContentHeight);

        canvas.Save();
        canvas.Translate(-camera.OffsetX, -camera.OffsetY);
        canvas.Scale(camera.Zoom);

        DrawRoads(canvas, state, layout);
        DrawNodes(canvas, state, layout, selectedTileId, attackTargets, time);
        if (feedback is not null) DrawFeedback(canvas, feedback);

        canvas.Restore();
    }

    private static void DrawParchment(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(0x39, 0x2e, 0x20));
        using var p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        int tile = Math.Max(24, info.Width / 22);
        for (int y = 0; y < info.Height; y += tile)
        for (int x = 0; x < info.Width; x += tile)
        {
            bool alt = ((x / tile) + (y / tile)) % 2 == 0;
            p.Color = alt ? new SKColor(0x3f, 0x33, 0x23) : new SKColor(0x37, 0x2c, 0x1e);
            canvas.DrawRect(x, y, tile, tile, p);
        }
        // 暗角
        using var vig = new SKPaint { IsAntialias = false };
        vig.Color = new SKColor(0, 0, 0, 60);
        canvas.DrawRect(0, 0, info.Width, 10, vig);
        canvas.DrawRect(0, info.Height - 10, info.Width, 10, vig);
    }

    private static void DrawRoads(SKCanvas canvas, GameState state, MapLayout layout)
    {
        using var outer = new SKPaint { Color = new SKColor(0x6b, 0x55, 0x33), StrokeWidth = 9, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        using var inner = new SKPaint { Color = new SKColor(0x97, 0x7b, 0x4c), StrokeWidth = 4, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };

        var drawn = new HashSet<string>();
        foreach (var tile in state.Tiles.Values)
        {
            if (!layout.Positions.TryGetValue(tile.Id, out var a)) continue;
            foreach (var adj in tile.Adjacent)
            {
                var key = string.CompareOrdinal(tile.Id, adj) < 0 ? $"{tile.Id}|{adj}" : $"{adj}|{tile.Id}";
                if (!drawn.Add(key)) continue;
                if (layout.Positions.TryGetValue(adj, out var b))
                {
                    canvas.DrawLine(a, b, outer);
                    canvas.DrawLine(a, b, inner);
                }
            }
        }
    }

    private static void DrawNodes(SKCanvas canvas, GameState state, MapLayout layout,
        string? selectedTileId, IReadOnlySet<string> attackTargets, float time)
    {
        float r = layout.NodeRadius;
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 4f);

        using var text = new SKPaint { IsAntialias = false, Color = new SKColor(0xef, 0xe2, 0xbd) };
        using var sub = new SKPaint { IsAntialias = false, Color = new SKColor(0xd7, 0xc6, 0x9a) };
        using var titleFont = new SKFont(PixelFont.Typeface, MathF.Max(13, r * 0.5f));
        using var subFont = new SKFont(PixelFont.Typeface, MathF.Max(11, r * 0.4f));

        foreach (var tile in state.Tiles.Values)
        {
            if (!layout.Positions.TryGetValue(tile.Id, out var p)) continue;
            var color = FactionColors.For(state, tile);

            // 高亮环（呼吸）
            if (tile.Id == selectedTileId)
                DrawPulseRing(canvas, p, r + 6, new SKColor(0xff, 0xe6, 0x9a), pulse);
            else if (attackTargets.Contains(tile.Id))
                DrawPulseRing(canvas, p, r + 6, new SKColor(0xff, 0x6a, 0x3a), pulse);

            // 平台底座（阵营底色）
            DrawBase(canvas, p, r, color);
            // 建筑
            DrawBuilding(canvas, p, r, tile.Type, color);
            // 阵营旗
            DrawFlag(canvas, p, r, color);

            // 文字
            DrawLabel(canvas, tile.Name, p.X, p.Y - r - r * 0.35f, titleFont, text);
            DrawLabel(canvas, $"将{tile.Generals.Count} 兵{tile.Units.Count}", p.X, p.Y + r + r * 0.7f, subFont, sub);
        }
    }

    private static void DrawPulseRing(SKCanvas canvas, SKPoint p, float radius, SKColor color, float pulse)
    {
        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 + 2 * pulse };
        ring.Color = color.WithAlpha((byte)(140 + 100 * pulse));
        canvas.DrawCircle(p, radius + 3 * pulse, ring);
    }

    private static void DrawBase(SKCanvas canvas, SKPoint p, float r, SKColor color)
    {
        using var pa = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        pa.Color = new SKColor(0x20, 0x18, 0x10);
        canvas.DrawOval(new SKRect(p.X - r, p.Y + r * 0.35f, p.X + r, p.Y + r * 0.85f), pa);
        pa.Color = Darken(color, 0.5f);
        canvas.DrawOval(new SKRect(p.X - r * 0.95f, p.Y + r * 0.3f, p.X + r * 0.95f, p.Y + r * 0.75f), pa);
    }

    private static void DrawBuilding(SKCanvas canvas, SKPoint p, float r, TileType type, SKColor color)
    {
        using var pa = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        float u = r / 8f;
        SKColor wall = new(0xcf, 0xc2, 0xa6);
        SKColor wallDark = new(0x9b, 0x8d, 0x71);
        SKColor roof = Darken(color, 0.85f);

        switch (type)
        {
            case TileType.City:
                // 城墙 + 城楼
                pa.Color = wallDark; canvas.DrawRect(p.X - 7 * u, p.Y - 1 * u, 14 * u, 5 * u, pa);
                pa.Color = wall; canvas.DrawRect(p.X - 7 * u, p.Y - 1 * u, 14 * u, 2 * u, pa);
                // 雉堞
                for (int i = -7; i <= 5; i += 3) { pa.Color = wall; canvas.DrawRect(p.X + i * u, p.Y - 2 * u, 2 * u, 1.5f * u, pa); }
                // 中央城楼
                pa.Color = wall; canvas.DrawRect(p.X - 3 * u, p.Y - 6 * u, 6 * u, 5 * u, pa);
                pa.Color = roof; canvas.DrawRect(p.X - 4 * u, p.Y - 8 * u, 8 * u, 2.5f * u, pa);
                pa.Color = Darken(roof, 0.8f); canvas.DrawRect(p.X - 1 * u, p.Y - 3 * u, 2 * u, 2 * u, pa);
                break;
            case TileType.Pass:
                // 关隘：两塔夹门
                pa.Color = wallDark; canvas.DrawRect(p.X - 7 * u, p.Y - 5 * u, 4 * u, 9 * u, pa);
                canvas.DrawRect(p.X + 3 * u, p.Y - 5 * u, 4 * u, 9 * u, pa);
                pa.Color = roof; canvas.DrawRect(p.X - 7.5f * u, p.Y - 6.5f * u, 5 * u, 2 * u, pa);
                canvas.DrawRect(p.X + 2.5f * u, p.Y - 6.5f * u, 5 * u, 2 * u, pa);
                pa.Color = new SKColor(0x2a, 0x20, 0x16); canvas.DrawRect(p.X - 3 * u, p.Y - 3 * u, 6 * u, 7 * u, pa);
                break;
            default: // Village
                pa.Color = wall; canvas.DrawRect(p.X - 5 * u, p.Y - 1 * u, 10 * u, 5 * u, pa);
                pa.Color = roof; canvas.DrawRect(p.X - 6 * u, p.Y - 4 * u, 12 * u, 3 * u, pa);
                pa.Color = new SKColor(0x3a, 0x2a, 0x1a); canvas.DrawRect(p.X - 1.5f * u, p.Y + 1 * u, 3 * u, 3 * u, pa);
                break;
        }
    }

    private static void DrawFlag(SKCanvas canvas, SKPoint p, float r, SKColor color)
    {
        using var pa = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        float u = r / 8f;
        float poleX = p.X + 5 * u, poleTop = p.Y - 11 * u;
        pa.Color = new SKColor(0x2a, 0x20, 0x16);
        canvas.DrawRect(poleX, poleTop, u * 0.8f, 6 * u, pa);
        pa.Color = color;
        canvas.DrawRect(poleX + u * 0.8f, poleTop, 4 * u, 3 * u, pa);
        pa.Color = Darken(color, 0.7f);
        canvas.DrawRect(poleX + u * 0.8f, poleTop + 2 * u, 4 * u, u, pa);
    }

    private static void DrawLabel(SKCanvas canvas, string s, float x, float y, SKFont font, SKPaint paint)
    {
        using var shadow = new SKPaint { IsAntialias = false, Color = new SKColor(0, 0, 0, 200) };
        canvas.DrawText(s, x + 1, y + 1, SKTextAlign.Center, font, shadow);
        canvas.DrawText(s, x, y, SKTextAlign.Center, font, paint);
    }

    private static void DrawFeedback(SKCanvas canvas, IReadOnlyList<FloatingText> feedback)
    {
        foreach (var ft in feedback)
        {
            using var font = new SKFont(PixelFont.Typeface, MathF.Max(14, ft.SizeFactor * 60));
            byte a = (byte)(255 * ft.Alpha);
            using var shadow = new SKPaint { IsAntialias = false, Color = new SKColor(0, 0, 0, (byte)(180 * ft.Alpha)) };
            using var text = new SKPaint { IsAntialias = false, Color = ft.Color.WithAlpha(a) };
            canvas.DrawText(ft.Text, ft.X + 1.5f, ft.Y + 1.5f, SKTextAlign.Center, font, shadow);
            canvas.DrawText(ft.Text, ft.X, ft.Y, SKTextAlign.Center, font, text);
        }
    }

    private static SKColor Darken(SKColor c, float f) =>
        new((byte)(c.Red * f), (byte)(c.Green * f), (byte)(c.Blue * f), c.Alpha);
}

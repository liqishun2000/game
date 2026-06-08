using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>大地图顶栏像素 HUD：月份徽 + 金/粮/科技/监狱 图标与数值。</summary>
public sealed class HudRenderer
{
    public void Draw(SKCanvas canvas, SKImageInfo info, int month, int gold, int food, int tech, int prison, float topInset = 0)
    {
        canvas.Clear(new SKColor(0x24, 0x1b, 0x14));
        float h = info.Height;
        float cy = topInset + (h - topInset) / 2f;
        using var edge = new SKPaint { IsAntialias = false, Color = new SKColor(0x9c, 0x6b, 0x1f), Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawLine(0, h - 1, info.Width, h - 1, edge);

        float icon = MathF.Min((h - topInset) * 0.5f, 22);
        float x = 12;

        using var font = new SKFont(PixelFont.Typeface, MathF.Max(13, h * 0.34f));
        using var tp = new SKPaint { IsAntialias = false, Color = new SKColor(0xef, 0xe2, 0xbd) };
        using var pa = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };

        // 月份徽
        pa.Color = new SKColor(0x9c, 0x6b, 0x1f);
        canvas.DrawRect(x, cy - icon * 0.7f, icon * 2.8f, icon * 1.4f, pa);
        pa.Color = new SKColor(0x2a, 0x20, 0x16);
        canvas.DrawRect(x + 2, cy - icon * 0.7f + 2, icon * 2.8f - 4, icon * 1.4f - 4, pa);
        canvas.DrawText($"{month}月", x + icon * 1.4f, cy + font.Size * 0.35f, SKTextAlign.Center, font, tp);
        x += icon * 3.4f;

        x = DrawStat(canvas, pa, font, tp, x, cy, icon, DrawCoin, gold.ToString());
        x = DrawStat(canvas, pa, font, tp, x, cy, icon, DrawWheat, food.ToString());
        x = DrawStat(canvas, pa, font, tp, x, cy, icon, DrawScroll, tech.ToString());
        x = DrawStat(canvas, pa, font, tp, x, cy, icon, DrawPrison, prison.ToString());
    }

    private float DrawStat(SKCanvas canvas, SKPaint pa, SKFont font, SKPaint tp,
        float x, float cy, float icon, Action<SKCanvas, SKPaint, float, float, float> drawIcon, string value)
    {
        drawIcon(canvas, pa, x, cy, icon);
        float tx = x + icon + 4;
        canvas.DrawText(value, tx, cy + font.Size * 0.35f, SKTextAlign.Left, font, tp);
        float adv = font.MeasureText(value);
        return tx + adv + icon * 0.9f;
    }

    private static void DrawCoin(SKCanvas c, SKPaint p, float x, float cy, float s)
    {
        float r = s / 2;
        p.Color = new SKColor(0x9c, 0x6b, 0x1f); c.DrawCircle(x + r, cy, r, p);
        p.Color = new SKColor(0xe8, 0xb9, 0x48); c.DrawCircle(x + r, cy, r * 0.8f, p);
        p.Color = new SKColor(0x9c, 0x6b, 0x1f); c.DrawRect(x + r - r * 0.15f, cy - r * 0.4f, r * 0.3f, r * 0.8f, p);
    }

    private static void DrawWheat(SKCanvas c, SKPaint p, float x, float cy, float s)
    {
        float r = s / 2;
        p.Color = new SKColor(0x6a, 0x9a, 0x3a); c.DrawRect(x + r - r * 0.12f, cy - r * 0.6f, r * 0.24f, r * 1.4f, p);
        p.Color = new SKColor(0xd0, 0xc0, 0x40);
        c.DrawRect(x + r - r * 0.6f, cy - r * 0.6f, r * 0.5f, r * 0.5f, p);
        c.DrawRect(x + r + r * 0.1f, cy - r * 0.6f, r * 0.5f, r * 0.5f, p);
        c.DrawRect(x + r - r * 0.25f, cy - r * 0.9f, r * 0.5f, r * 0.5f, p);
    }

    private static void DrawScroll(SKCanvas c, SKPaint p, float x, float cy, float s)
    {
        float r = s / 2;
        p.Color = new SKColor(0xe8, 0xd9, 0xb0); c.DrawRect(x + r * 0.3f, cy - r * 0.7f, r * 1.4f, r * 1.4f, p);
        p.Color = new SKColor(0x9c, 0x6b, 0x1f);
        c.DrawRect(x + r * 0.3f, cy - r * 0.7f, r * 1.4f, r * 0.25f, p);
        c.DrawRect(x + r * 0.3f, cy + r * 0.45f, r * 1.4f, r * 0.25f, p);
    }

    private static void DrawPrison(SKCanvas c, SKPaint p, float x, float cy, float s)
    {
        float r = s / 2;
        p.Color = new SKColor(0x5b, 0x6b, 0x7a); c.DrawRect(x, cy - r * 0.8f, s, r * 1.6f, p);
        p.Color = new SKColor(0x20, 0x18, 0x10);
        for (float i = 0; i <= 3; i++)
            c.DrawRect(x + i * (s / 3.3f), cy - r * 0.8f, r * 0.18f, r * 1.6f, p);
    }
}

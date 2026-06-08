using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>
/// 主菜单程序化像素背景：黄昏天幕 + 远山剪影 + 落日 + 飘云 + 战旗。
/// 在低分辨率虚拟画布（192×108）上作画再整体放大，得到统一的像素观感（最近邻）。
/// 由 <see cref="AnimationClock"/> 传入时间 t 做云朵漂移与战旗轻摆。
/// </summary>
public sealed class MenuBackgroundRenderer
{
    private const int VW = 192;
    private const int VH = 108;

    public void Draw(SKCanvas canvas, SKImageInfo info, float t)
    {
        canvas.Clear(new SKColor(0x16, 0x11, 0x0d));
        // 横屏下拉伸铺满，避免 cover 裁切只露出天空条带
        canvas.Save();
        canvas.Scale(info.Width / (float)VW, info.Height / (float)VH);

        DrawSky(canvas);
        DrawSun(canvas, t);
        DrawClouds(canvas, t);
        DrawMountains(canvas, 70, new SKColor(0x4a, 0x35, 0x55), 26, 0.6f);
        DrawMountains(canvas, 82, new SKColor(0x35, 0x26, 0x3f), 34, 1.0f);
        DrawGround(canvas);
        DrawBanner(canvas, 30, 70, new SKColor(0xb2, 0x3a, 0x36), t, 0f);
        DrawBanner(canvas, 162, 70, new SKColor(0x3a, 0x8a, 0x4a), t, 1.3f);

        canvas.Restore();
    }

    private static void DrawSky(SKCanvas canvas)
    {
        // 黄昏渐变：顶部深靛 -> 地平线暖橙，分若干像素条带
        SKColor[] bands =
        {
            new(0x23, 0x1b, 0x3a), new(0x33, 0x22, 0x44), new(0x4a, 0x2e, 0x4e),
            new(0x6e, 0x3a, 0x4a), new(0x9c, 0x4f, 0x42), new(0xc9, 0x6b, 0x3c),
            new(0xe2, 0x8b, 0x3e),
        };
        using var p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        int h = 84;
        float bandH = h / (float)bands.Length;
        for (int i = 0; i < bands.Length; i++)
        {
            p.Color = bands[i];
            canvas.DrawRect(0, i * bandH, VW, bandH + 1, p);
        }
    }

    private static void DrawSun(SKCanvas canvas, float t)
    {
        using var p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        float pulse = 0.5f + 0.5f * MathF.Sin(t * 1.2f);
        p.Color = new SKColor(0xff, 0xd9, 0x7a, (byte)(40 + 30 * pulse));
        canvas.DrawCircle(96, 60, 26, p);
        p.Color = new SKColor(0xff, 0xcf, 0x66);
        canvas.DrawCircle(96, 60, 16, p);
        p.Color = new SKColor(0xff, 0xe6, 0x9a);
        canvas.DrawCircle(96, 60, 11, p);
    }

    private static void DrawClouds(SKCanvas canvas, float t)
    {
        using var p = new SKPaint { IsAntialias = false, Color = new SKColor(0xe6, 0xb8, 0x86, 0xaa) };
        float drift = (t * 6f) % (VW + 60);
        DrawCloud(canvas, p, (drift) % (VW + 60) - 30, 24);
        DrawCloud(canvas, p, (drift + 90) % (VW + 60) - 30, 40);
        DrawCloud(canvas, p, (drift + 150) % (VW + 60) - 30, 16);
    }

    private static void DrawCloud(SKCanvas canvas, SKPaint p, float x, float y)
    {
        canvas.DrawRect(x, y, 22, 4, p);
        canvas.DrawRect(x + 4, y - 3, 14, 4, p);
    }

    private static void DrawMountains(SKCanvas canvas, int baseY, SKColor color, int peak, float seedShift)
    {
        using var p = new SKPaint { IsAntialias = false, Color = color, Style = SKPaintStyle.Fill };
        using var path = new SKPath();
        path.MoveTo(0, VH);
        path.LineTo(0, baseY);
        int x = 0;
        int i = 0;
        while (x <= VW)
        {
            int span = 26 + (int)(10 * MathF.Sin((i + seedShift) * 1.7f));
            int up = (int)(peak * (0.6f + 0.4f * MathF.Sin((i + seedShift) * 2.3f)));
            path.LineTo(x + span / 2, baseY - up);
            path.LineTo(x + span, baseY);
            x += span;
            i++;
        }
        path.LineTo(VW, VH);
        path.Close();
        canvas.DrawPath(path, p);
    }

    private static void DrawGround(SKCanvas canvas)
    {
        using var p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        p.Color = new SKColor(0x24, 0x2e, 0x1c);
        canvas.DrawRect(0, 82, VW, VH - 82, p);
        p.Color = new SKColor(0x1b, 0x24, 0x15);
        canvas.DrawRect(0, 92, VW, VH - 92, p);
    }

    private static void DrawBanner(SKCanvas canvas, int x, int groundY, SKColor color, float t, float phase)
    {
        using var pole = new SKPaint { IsAntialias = false, Color = new SKColor(0x3a, 0x2b, 0x1c) };
        canvas.DrawRect(x, groundY - 36, 2, 36, pole);

        using var flag = new SKPaint { IsAntialias = false, Color = color };
        float sway = MathF.Sin(t * 2f + phase) * 1.5f;
        for (int row = 0; row < 10; row++)
        {
            float w = 14 - MathF.Abs(row - 5) * 0.6f + sway * (row / 10f);
            canvas.DrawRect(x + 2, groundY - 34 + row, w, 1, flag);
        }
        using var trim = new SKPaint { IsAntialias = false, Color = new SKColor(0xe8, 0xb9, 0x48) };
        canvas.DrawRect(x + 2, groundY - 34, 14, 1, trim);
    }
}

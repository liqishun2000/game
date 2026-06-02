using MauiApp.Game.Battle;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>战斗方格战场绘制：网格、可达高亮、单位、血条、当前行动者。</summary>
public sealed class BattleRenderer
{
    public float CellSize { get; private set; }
    public float OriginX { get; private set; }
    public float OriginY { get; private set; }

    public void Draw(SKCanvas canvas, SKImageInfo info, BattleState state,
        BattleUnit? current, IReadOnlySet<(int Col, int Row)> reachable, IReadOnlySet<int> attackable)
    {
        canvas.Clear(new SKColor(0x16, 0x1a, 0x12));
        if (state.Width == 0 || state.Height == 0) return;

        CellSize = Math.Min((float)info.Width / state.Width, (float)info.Height / state.Height);
        OriginX = (info.Width - CellSize * state.Width) / 2;
        OriginY = (info.Height - CellSize * state.Height) / 2;

        DrawGrid(canvas, state, reachable);
        DrawUnits(canvas, state, current, attackable);
    }

    public (int Col, int Row)? HitTest(BattleState state, float x, float y)
    {
        if (CellSize <= 0) return null;
        int col = (int)((x - OriginX) / CellSize);
        int row = (int)((y - OriginY) / CellSize);
        return state.InBounds(col, row) ? (col, row) : null;
    }

    private void DrawGrid(SKCanvas canvas, BattleState state, IReadOnlySet<(int, int)> reachable)
    {
        using var line = new SKPaint { Color = new SKColor(0x33, 0x3a, 0x2c), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        using var reach = new SKPaint { Color = new SKColor(0x2f, 0x6f, 0xed, 0x55), Style = SKPaintStyle.Fill };

        for (int c = 0; c < state.Width; c++)
        for (int r = 0; r < state.Height; r++)
        {
            var rect = CellRect(c, r);
            if (reachable.Contains((c, r))) canvas.DrawRect(rect, reach);
            canvas.DrawRect(rect, line);
        }
    }

    private void DrawUnits(SKCanvas canvas, BattleState state, BattleUnit? current, IReadOnlySet<int> attackable)
    {
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
        using var hpBg = new SKPaint { Color = new SKColor(0x40, 0x40, 0x40), Style = SKPaintStyle.Fill };
        using var hp = new SKPaint { Style = SKPaintStyle.Fill };
        using var text = new SKPaint { IsAntialias = true, Color = SKColors.White, TextSize = 13, TextAlign = SKTextAlign.Center };

        foreach (var u in state.Units.Where(u => u.IsAlive))
        {
            var rect = CellRect(u.Col, u.Row);
            var center = new SKPoint(rect.MidX, rect.MidY);
            float radius = CellSize * 0.34f;

            fill.Color = u.Side == BattleSide.Attacker
                ? new SKColor(0x3a, 0xa0, 0x4a)
                : new SKColor(0xd0, 0x3a, 0x3a);
            if (u.IsGeneral) fill.Color = u.Side == BattleSide.Attacker
                ? new SKColor(0x2f, 0x6f, 0xed)
                : new SKColor(0xb0, 0x2a, 0x8a);
            canvas.DrawCircle(center, radius, fill);

            if (current is not null && current.Id == u.Id)
            {
                ring.Color = SKColors.Gold;
                canvas.DrawCircle(center, radius + 4, ring);
            }
            else if (attackable.Contains(u.Id))
            {
                ring.Color = new SKColor(0xff, 0xb0, 0x3a);
                canvas.DrawCircle(center, radius + 4, ring);
            }

            // 血条
            float bw = CellSize * 0.7f, bx = center.X - bw / 2, by = center.Y + radius + 3;
            canvas.DrawRect(bx, by, bw, 5, hpBg);
            float ratio = u.MaxHp > 0 ? Math.Clamp((float)u.CurHp / u.MaxHp, 0, 1) : 0;
            hp.Color = ratio > 0.5f ? new SKColor(0x4c, 0xd0, 0x5a) : ratio > 0.2f ? new SKColor(0xe0, 0xc0, 0x40) : new SKColor(0xe0, 0x50, 0x40);
            canvas.DrawRect(bx, by, bw * ratio, 5, hp);

            canvas.DrawText(u.IsGeneral ? u.Name : "兵", center.X, center.Y + 4, text);
        }
    }

    private SKRect CellRect(int col, int row) =>
        new(OriginX + col * CellSize, OriginY + row * CellSize,
            OriginX + (col + 1) * CellSize, OriginY + (row + 1) * CellSize);
}

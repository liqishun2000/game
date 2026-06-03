using MauiApp.Game.Battle;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>
/// 战斗方格战场像素绘制：地形 tile、可达/可攻击高亮（呼吸）、像素单位 sprite（含朝向/将领旗）、
/// 血条、当前行动者、回合行动序条；并消费 <see cref="BattleVfx"/> 演出移动/冲刺/闪白/飘字/阵亡/震屏。
/// </summary>
public sealed class BattleRenderer
{
    public float CellSize { get; private set; }
    public float OriginX { get; private set; }
    public float OriginY { get; private set; }

    private static readonly SKColor Grass = new(0x3b, 0x4a, 0x2a);
    private static readonly SKColor GrassAlt = new(0x44, 0x55, 0x30);
    private static readonly SKColor GrassEdge = new(0x2c, 0x38, 0x20);

    public void Draw(SKCanvas canvas, SKImageInfo info, BattleState state,
        BattleUnit? current, IReadOnlySet<(int Col, int Row)> reachable, IReadOnlySet<int> attackable,
        BattleVfx vfx, float time)
    {
        canvas.Clear(new SKColor(0x12, 0x16, 0x10));
        if (state.Width == 0 || state.Height == 0) return;

        float barH = MathF.Min(54, info.Height * 0.12f);
        CellSize = Math.Min(info.Width / (float)state.Width, (info.Height - barH) / state.Height);
        OriginX = (info.Width - CellSize * state.Width) / 2;
        OriginY = barH + (info.Height - barH - CellSize * state.Height) / 2;

        canvas.Save();
        canvas.Translate(vfx.Shake.X, vfx.Shake.Y);

        DrawTerrain(canvas, state);
        DrawHighlights(canvas, state, reachable, attackable, time);
        DrawUnits(canvas, state, current, vfx);
        DrawDying(canvas, vfx);
        DrawFloatingTexts(canvas, vfx);

        canvas.Restore();

        DrawTurnBar(canvas, info, state, current, barH);
    }

    public (int Col, int Row)? HitTest(BattleState state, float x, float y)
    {
        if (CellSize <= 0) return null;
        int col = (int)((x - OriginX) / CellSize);
        int row = (int)((y - OriginY) / CellSize);
        return state.InBounds(col, row) ? (col, row) : null;
    }

    // ---------- 地形 ----------
    private void DrawTerrain(SKCanvas canvas, BattleState state)
    {
        using var fill = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        using var edge = new SKPaint { IsAntialias = false, Color = GrassEdge, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        for (int c = 0; c < state.Width; c++)
        for (int r = 0; r < state.Height; r++)
        {
            var rect = CellRect(c, r);
            fill.Color = (c + r) % 2 == 0 ? Grass : GrassAlt;
            canvas.DrawRect(rect, fill);
            // 像素草点缀
            if (((c * 7 + r * 13) % 5) == 0)
            {
                fill.Color = GrassEdge;
                float u = CellSize / 12f;
                canvas.DrawRect(rect.Left + CellSize * 0.3f, rect.Top + CellSize * 0.6f, u, u, fill);
                canvas.DrawRect(rect.Left + CellSize * 0.6f, rect.Top + CellSize * 0.35f, u, u, fill);
            }
            canvas.DrawRect(rect, edge);
        }
    }

    private void DrawHighlights(SKCanvas canvas, BattleState state,
        IReadOnlySet<(int, int)> reachable, IReadOnlySet<int> attackable, float time)
    {
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 4f);
        using var reach = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        using var reachEdge = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };

        foreach (var (c, r) in reachable)
        {
            var rect = CellRect(c, r);
            rect.Inflate(-1, -1);
            reach.Color = new SKColor(0x4c, 0x8a, 0xff, (byte)(40 + 30 * pulse));
            canvas.DrawRect(rect, reach);
            reachEdge.Color = new SKColor(0x6f, 0xa6, 0xff, (byte)(120 + 80 * pulse));
            canvas.DrawRect(rect, reachEdge);
        }

        using var atk = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
        foreach (var u in state.Units.Where(u => u.IsAlive && attackable.Contains(u.Id)))
        {
            var rect = CellRect(u.Col, u.Row);
            rect.Inflate(-2, -2);
            atk.Color = new SKColor(0xff, 0x6a, 0x3a, (byte)(150 + 90 * pulse));
            canvas.DrawRect(rect, atk);
        }
    }

    // ---------- 单位 ----------
    private void DrawUnits(SKCanvas canvas, BattleState state, BattleUnit? current, BattleVfx vfx)
    {
        foreach (var u in state.Units.Where(u => u.IsAlive).OrderBy(u => u.Row).ThenBy(u => u.Col))
        {
            var off = vfx.OffsetOf(u.Id);
            float cx = OriginX + (u.Col + 0.5f + off.X) * CellSize;
            float cy = OriginY + (u.Row + 0.5f + off.Y) * CellSize;

            bool facingRight = FacingRight(state, u);
            bool isCurrent = current is not null && current.Id == u.Id;
            DrawUnitSprite(canvas, cx, cy, u.Side, u.IsGeneral, facingRight, vfx.FlashOf(u.Id), 1f, isCurrent);
            DrawHpBar(canvas, cx, cy, u.MaxHp > 0 ? (float)u.CurHp / u.MaxHp : 0f);
            if (u.IsGeneral) DrawName(canvas, cx, cy, u.Name);
        }
    }

    private void DrawDying(SKCanvas canvas, BattleVfx vfx)
    {
        foreach (var d in vfx.Dying)
        {
            float cx = OriginX + (d.Col + 0.5f) * CellSize;
            float cy = OriginY + (d.Row + 0.5f) * CellSize;
            DrawUnitSprite(canvas, cx, cy, d.Side, d.IsGeneral, true, 0f, d.Alpha, false);
        }
    }

    private bool FacingRight(BattleState state, BattleUnit u)
    {
        var enemy = state.Units.Where(e => e.IsAlive && e.Side != u.Side)
            .OrderBy(e => Math.Abs(e.Col - u.Col) + Math.Abs(e.Row - u.Row)).FirstOrDefault();
        return enemy is null ? u.Side == BattleSide.Attacker : enemy.Col >= u.Col;
    }

    private void DrawUnitSprite(SKCanvas canvas, float cx, float cy,
        BattleSide side, bool general, bool facingRight, float flash, float alpha, bool current)
    {
        float s = CellSize;
        byte a = (byte)(255 * Math.Clamp(alpha, 0, 1));
        float px = s / 16f; // 像素单元

        SKColor body = side == BattleSide.Attacker ? new SKColor(0x3f, 0x8f, 0x4f) : new SKColor(0xc0, 0x44, 0x40);
        SKColor bodyDark = side == BattleSide.Attacker ? new SKColor(0x28, 0x63, 0x36) : new SKColor(0x86, 0x2c, 0x2a);
        if (general)
        {
            body = side == BattleSide.Attacker ? new SKColor(0x4c, 0xa8, 0xff) : new SKColor(0xc6, 0x52, 0xc0);
            bodyDark = side == BattleSide.Attacker ? new SKColor(0x2f, 0x6f, 0xed) : new SKColor(0x8a, 0x32, 0x86);
        }

        using var p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };

        // 阴影
        p.Color = new SKColor(0, 0, 0, (byte)(70 * alpha));
        canvas.DrawOval(new SKRect(cx - 5 * px, cy + 5 * px, cx + 5 * px, cy + 7.5f * px), p);

        float scale = general ? 1.15f : 1f;
        float w = 8 * px * scale, h = 9 * px * scale;
        float left = cx - w / 2, top = cy - h / 2 - px;

        // 当前行动者底环
        if (current)
        {
            p.Color = new SKColor(0xff, 0xd1, 0x40, 200);
            canvas.DrawOval(new SKRect(cx - 6 * px, cy + 4.5f * px, cx + 6 * px, cy + 8 * px), p);
        }

        // 身体
        p.Color = WithAlpha(body, a);
        canvas.DrawRect(left, top + h * 0.4f, w, h * 0.55f, p);
        p.Color = WithAlpha(bodyDark, a);
        canvas.DrawRect(left, top + h * 0.78f, w, h * 0.17f, p);

        // 头
        p.Color = WithAlpha(new SKColor(0xe8, 0xc4, 0x9a), a);
        canvas.DrawRect(cx - 3 * px, top + h * 0.12f, 6 * px, h * 0.3f, p);

        // 头盔（将领金盔）
        p.Color = WithAlpha(general ? new SKColor(0xe8, 0xb9, 0x48) : bodyDark, a);
        canvas.DrawRect(cx - 3.5f * px, top, 7 * px, h * 0.16f, p);

        // 武器（长枪，朝向）
        p.Color = WithAlpha(new SKColor(0xd9, 0xd9, 0xd9), a);
        float dir = facingRight ? 1 : -1;
        float spx = cx + dir * (w / 2);
        canvas.DrawRect(facingRight ? spx : spx - px, top - 2 * px, px, h * 0.95f, p);
        p.Color = WithAlpha(new SKColor(0xff, 0xe6, 0x9a), a);
        canvas.DrawRect(facingRight ? spx : spx - 1.5f * px, top - 2.5f * px, 1.5f * px, 1.5f * px, p);

        // 将领背旗
        if (general)
        {
            p.Color = WithAlpha(side == BattleSide.Attacker ? new SKColor(0xe8, 0xb9, 0x48) : new SKColor(0xff, 0xd1, 0x40), a);
            float fx = cx - dir * (w / 2) - (facingRight ? 3 * px : 0);
            canvas.DrawRect(cx - dir * (w / 2 + px), top - 4 * px, px, 6 * px, p); // 旗杆
            canvas.DrawRect(fx, top - 4 * px, 3 * px, 3 * px, p); // 旗面
        }

        // 受击闪白
        if (flash > 0.01f)
        {
            p.Color = new SKColor(0xff, 0xff, 0xff, (byte)(200 * Math.Clamp(flash, 0, 1)));
            canvas.DrawRect(left, top, w, h, p);
        }
    }

    private void DrawHpBar(SKCanvas canvas, float cx, float cy, float ratio)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        float w = CellSize * 0.62f, h = MathF.Max(3, CellSize * 0.055f);
        float x = cx - w / 2, y = cy + CellSize * 0.36f;
        using var p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        p.Color = new SKColor(0x10, 0x10, 0x10); canvas.DrawRect(x - 1, y - 1, w + 2, h + 2, p);
        p.Color = new SKColor(0x3a, 0x3a, 0x3a); canvas.DrawRect(x, y, w, h, p);
        p.Color = ratio > 0.5f ? new SKColor(0x5a, 0xd0, 0x5a) : ratio > 0.25f ? new SKColor(0xe0, 0xc0, 0x40) : new SKColor(0xe0, 0x50, 0x40);
        canvas.DrawRect(x, y, w * ratio, h, p);
    }

    private void DrawName(SKCanvas canvas, float cx, float cy, string name)
    {
        using var font = new SKFont(PixelFont.Typeface, MathF.Max(10, CellSize * 0.2f));
        using var shadow = new SKPaint { IsAntialias = false, Color = new SKColor(0, 0, 0, 200) };
        using var text = new SKPaint { IsAntialias = false, Color = new SKColor(0xff, 0xe6, 0x9a) };
        float y = cy - CellSize * 0.42f;
        canvas.DrawText(name, cx + 1, y + 1, SKTextAlign.Center, font, shadow);
        canvas.DrawText(name, cx, y, SKTextAlign.Center, font, text);
    }

    private void DrawFloatingTexts(SKCanvas canvas, BattleVfx vfx)
    {
        foreach (var ft in vfx.Texts)
        {
            float x = OriginX + ft.X * CellSize;
            float y = OriginY + ft.Y * CellSize;
            float size = MathF.Max(12, CellSize * ft.SizeFactor);
            using var font = new SKFont(PixelFont.Typeface, size);
            byte a = (byte)(255 * ft.Alpha);
            using var shadow = new SKPaint { IsAntialias = false, Color = new SKColor(0, 0, 0, (byte)(180 * ft.Alpha)) };
            using var text = new SKPaint { IsAntialias = false, Color = WithAlpha(ft.Color, a) };
            canvas.DrawText(ft.Text, x + 1.5f, y + 1.5f, SKTextAlign.Center, font, shadow);
            canvas.DrawText(ft.Text, x, y, SKTextAlign.Center, font, text);
        }
    }

    // ---------- 行动序条 ----------
    private void DrawTurnBar(SKCanvas canvas, SKImageInfo info, BattleState state, BattleUnit? current, float barH)
    {
        using var bg = new SKPaint { IsAntialias = false, Color = new SKColor(0x20, 0x18, 0x10) };
        canvas.DrawRect(0, 0, info.Width, barH, bg);
        using var edge = new SKPaint { IsAntialias = false, Color = new SKColor(0x9c, 0x6b, 0x1f), Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawLine(0, barH, info.Width, barH, edge);

        var order = state.PendingOrder
            .Select(state.GetUnit)
            .Where(u => u is { IsAlive: true })
            .Cast<BattleUnit>()
            .Take(12)
            .ToList();

        float chip = MathF.Min(barH - 12, 38);
        float x = 10;
        float y = (barH - chip) / 2;
        using var p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        using var ring = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
        using var font = new SKFont(PixelFont.Typeface, chip * 0.5f);
        using var tp = new SKPaint { IsAntialias = false, Color = SKColors.White };

        foreach (var u in order)
        {
            var rect = new SKRect(x, y, x + chip, y + chip);
            p.Color = u.Side == BattleSide.Attacker ? new SKColor(0x2f, 0x6f, 0x3a) : new SKColor(0x8a, 0x32, 0x30);
            if (u.IsGeneral) p.Color = u.Side == BattleSide.Attacker ? new SKColor(0x2f, 0x6f, 0xed) : new SKColor(0xb0, 0x3a, 0x8a);
            canvas.DrawRect(rect, p);

            string label = u.IsGeneral ? u.Name.Substring(0, 1) : "兵";
            canvas.DrawText(label, rect.MidX, rect.MidY + chip * 0.18f, SKTextAlign.Center, font, tp);

            if (current is not null && current.Id == u.Id)
            {
                ring.Color = SKColors.Gold;
                canvas.DrawRect(rect, ring);
            }
            x += chip + 6;
            if (x > info.Width - chip) break;
        }
    }

    private SKRect CellRect(int col, int row) =>
        new(OriginX + col * CellSize, OriginY + row * CellSize,
            OriginX + (col + 1) * CellSize, OriginY + (row + 1) * CellSize);

    private static SKColor WithAlpha(SKColor c, byte a) => c.WithAlpha((byte)(c.Alpha * a / 255));
}

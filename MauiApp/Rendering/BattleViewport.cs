using MauiApp.Game.Battle;

namespace MauiApp.Rendering;

/// <summary>战斗地图视口与可见格范围（配合 MapCamera 做裁剪）。</summary>
public static class BattleViewport
{
    public readonly record struct CellRange(int Col0, int Col1, int Row0, int Row1);

    public static CellRange VisibleCells(
        BattleState state, float viewportW, float viewportH, MapCamera camera, float topBarH, float cellSize)
    {
        if (cellSize <= 0 || state.Width == 0 || state.Height == 0)
            return new CellRange(0, -1, 0, -1);

        float vLeft = camera.OffsetX / camera.Zoom;
        float vTop = camera.OffsetY / camera.Zoom;
        float vRight = vLeft + viewportW / camera.Zoom;
        float vBottom = vTop + (viewportH - topBarH) / camera.Zoom;

        int c0 = Math.Max(0, (int)(vLeft / cellSize) - 1);
        int c1 = Math.Min(state.Width - 1, (int)(vRight / cellSize) + 1);
        int r0 = Math.Max(0, (int)(vTop / cellSize) - 1);
        int r1 = Math.Min(state.Height - 1, (int)(vBottom / cellSize) + 1);
        return new CellRange(c0, c1, r0, r1);
    }

    public static bool Contains(CellRange range, int col, int row) =>
        col >= range.Col0 && col <= range.Col1 && row >= range.Row0 && row <= range.Row1;
}

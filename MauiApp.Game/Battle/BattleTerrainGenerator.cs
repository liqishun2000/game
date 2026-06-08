using MauiApp.Game.Stats;

namespace MauiApp.Game.Battle;

/// <summary>按 seed 生成战场地形（50×50 默认可玩布局）。</summary>
public static class BattleTerrainGenerator
{
    public static BattleTerrain[,] Generate(int width, int height, int seed, string mode = "procedural_v1")
    {
        var grid = new BattleTerrain[width, height];

        if (mode == "flat")
        {
            for (int c = 0; c < width; c++)
            for (int r = 0; r < height; r++)
                grid[c, r] = BattleTerrain.Plain;
            return grid;
        }

        if (mode != "procedural_v1")
            return grid;

        var rng = new DeterministicRandom(seed);
        int midC = width / 2;
        for (int r = 0; r < height; r++)
        {
            if (r % 3 == 1) grid[midC, r] = BattleTerrain.Road;
            if (midC > 0) grid[midC - 1, r] = BattleTerrain.Road;
        }

        // 随机簇：林/水/山
        int patches = Math.Max(8, width * height / 200);
        for (int i = 0; i < patches; i++)
        {
            int pc = rng.Next(Math.Max(1, width - 4)) + 2;
            int pr = rng.Next(Math.Max(1, height - 4)) + 2;
            var kind = (BattleTerrain)(rng.Next(3) + 1);
            int radius = rng.Next(3) + 1;
            for (int dc = -radius; dc <= radius; dc++)
            for (int dr = -radius; dr <= radius; dr++)
            {
                int c = pc + dc, r = pr + dr;
                if (c < 1 || r < 1 || c >= width - 1 || r >= height - 1) continue;
                if (IsSpawnZone(c, r, width, height)) continue;
                if (dc * dc + dr * dr <= radius * radius)
                    grid[c, r] = kind;
            }
        }

        // 左右出生区标记为 Fort（可通行，略增防）
        for (int r = 0; r < height; r++)
        for (int c = 0; c < Math.Min(4, width / 6); c++)
            grid[c, r] = BattleTerrain.Fort;
        for (int r = 0; r < height; r++)
        for (int c = width - Math.Min(4, width / 6); c < width; c++)
            grid[c, r] = BattleTerrain.Fort;

        return grid;
    }

    private static bool IsSpawnZone(int c, int r, int width, int height) =>
        c < 4 || c >= width - 4;
}

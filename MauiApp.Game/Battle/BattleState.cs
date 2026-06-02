namespace MauiApp.Game.Battle;

/// <summary>一场战斗的运行态（方格战场）。</summary>
public sealed class BattleState
{
    public int Width { get; set; }
    public int Height { get; set; }

    public List<BattleUnit> Units { get; } = new();

    /// <summary>当前回合数（从 1 开始）。</summary>
    public int Round { get; set; }
    public int MaxRounds { get; set; } = 30;

    /// <summary>人类玩家控制的阵营。</summary>
    public BattleSide PlayerSide { get; set; } = BattleSide.Attacker;

    /// <summary>本回合尚未行动的单位 id 顺序（按速度降序）。</summary>
    public List<int> PendingOrder { get; } = new();

    /// <summary>开局各阵营的小兵数量（用于俘获中"残兵比例"计算）。</summary>
    public Dictionary<BattleSide, int> InitialSoldierCount { get; } = new();

    public BattleUnit? UnitAt(int col, int row) =>
        Units.FirstOrDefault(u => u.IsAlive && u.Col == col && u.Row == row);

    public BattleUnit? GetUnit(int id) => Units.FirstOrDefault(u => u.Id == id);

    public IEnumerable<BattleUnit> AliveOf(BattleSide side) =>
        Units.Where(u => u.IsAlive && u.Side == side);

    public bool InBounds(int col, int row) =>
        col >= 0 && row >= 0 && col < Width && row < Height;
}

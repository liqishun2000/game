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

    /// <summary>各方入场区纵深（格数），用于撤退判定。</summary>
    public int SpawnDepth { get; set; } = BattleFactory.DefaultSpawnDepth;

    /// <summary>战场地形；null 时视为全平地。</summary>
    public BattleTerrain[,]? Terrain { get; set; }

    /// <summary>各方携带粮草（战斗内逐回合消耗）。</summary>
    public Dictionary<BattleSide, int> SideFood { get; } = new();

    /// <summary>各方连续断粮回合数。</summary>
    public Dictionary<BattleSide, int> StarvationRounds { get; } = new();

    public bool IsStarted { get; set; }

    public BattleTerrain GetTerrain(int col, int row) =>
        Terrain is not null && InBounds(col, row) ? Terrain[col, row] : BattleTerrain.Plain;

    public static bool IsPassable(BattleTerrain t) =>
        t is not BattleTerrain.Water and not BattleTerrain.Mountain;

    public static int MoveCost(BattleTerrain t) => t switch
    {
        BattleTerrain.Forest => 2,
        BattleTerrain.Mountain => 99,
        BattleTerrain.Water => 99,
        _ => 1,
    };

    /// <summary>该阵营的入场/撤退边缘区（进攻方左侧，防守方右侧）。</summary>
    public bool IsExitTile(BattleSide side, int col, int row)
    {
        if (!InBounds(col, row) || !IsPassable(GetTerrain(col, row))) return false;
        int depth = EffectiveSpawnDepth();
        return side == BattleSide.Attacker ? col < depth : col >= Width - depth;
    }

    public int EffectiveSpawnDepth() => Math.Max(1, Math.Min(SpawnDepth, Width / 6));

    public bool CanRetreat(BattleUnit unit) =>
        unit.IsGeneral && unit.IsAlive && IsExitTile(unit.Side, unit.Col, unit.Row);

    /// <summary>该格是否属于指定阵营的出生/布阵区。</summary>
    public bool IsSpawnTile(BattleSide side, int col, int row) => IsExitTile(side, col, row);
}

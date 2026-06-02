namespace MauiApp.Game.Battle;

/// <summary>战斗结束原因。</summary>
public enum BattleOutcome
{
    InProgress,
    AttackerWins,
    DefenderWins,
    Timeout,
}

/// <summary>被俘武将记录。</summary>
public sealed class CapturedGeneral
{
    public string GeneralTemplateId { get; set; } = "";
    public BattleSide CapturedBy { get; set; }
}

/// <summary>掉落装备记录。</summary>
public sealed class DroppedEquipment
{
    public string EquipmentId { get; set; } = "";
    public BattleSide ToSide { get; set; }
}

/// <summary>战斗结果（战后回写大地图，详见 04-battle.md 第 10 节）。</summary>
public sealed class BattleResult
{
    public BattleOutcome Outcome { get; set; } = BattleOutcome.InProgress;
    public int Rounds { get; set; }

    /// <summary>阵亡/被击溃的单位 id。</summary>
    public List<int> Fallen { get; } = new();

    /// <summary>被俘武将（含俘获方）。</summary>
    public List<CapturedGeneral> Captured { get; } = new();

    /// <summary>逃脱（未被俘也未阵亡处理）的武将模板 id。</summary>
    public List<string> EscapedGenerals { get; } = new();

    /// <summary>战死的武将模板 id。</summary>
    public List<string> KilledGenerals { get; } = new();

    /// <summary>掉落装备（含拾取方）。</summary>
    public List<DroppedEquipment> Drops { get; } = new();

    public bool AttackerWon => Outcome == BattleOutcome.AttackerWins;
    public bool DefenderWon => Outcome == BattleOutcome.DefenderWins;
    public bool Finished => Outcome != BattleOutcome.InProgress;
}

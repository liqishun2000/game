namespace MauiApp.Game.Stats;

/// <summary>
/// 平衡数值常量集中处（对应设计文档 05-stats-formulas.md）。
/// 后续可改为从外置 JSON 加载，便于不改代码调平衡。
/// </summary>
public sealed class BalanceConfig
{
    public static BalanceConfig Default { get; } = new();

    // 武将战场六维派生系数（05 第 2 节）
    public double HpBase { get; init; } = 200;
    public double HpPerTongshuai { get; init; } = 6;
    public double HpPerWuli { get; init; } = 4;
    public double HpPerLevel { get; init; } = 30;

    // 俘获（05 第 8 节）
    public double CaptureBase { get; init; } = 0.35;
    public double CapturePerSurround { get; init; } = 0.10;
    public double CaptureFleeingPenalty { get; init; } = -0.25;

    // 招降（05 第 9 节）
    public double PersuadeBase { get; init; } = 0.10;
    public double PersuadePerDetainMonth { get; init; } = 0.03;

    // 战斗
    public int BattleMaxRounds { get; init; } = 30;
}

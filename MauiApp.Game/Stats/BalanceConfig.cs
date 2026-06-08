namespace MauiApp.Game.Stats;

/// <summary>
/// 平衡数值常量集中处（对应设计文档 05-stats-formulas.md）。
/// 后续可改为从外置 JSON 加载，便于不改代码调平衡。
/// </summary>
public sealed class BalanceConfig
{
    public static BalanceConfig Default { get; } = new();

    // ---- 武将战场六维派生（05 第 2 节）----
    public double HpBase { get; init; } = 200;
    public double HpPerTongshuai { get; init; } = 6;
    public double HpPerWuli { get; init; } = 4;
    public double HpPerLevel { get; init; } = 30;

    public double PAtkBase { get; init; } = 20;
    public double PAtkPerWuli { get; init; } = 1.5;
    public double PAtkPerTongshuai { get; init; } = 0.3;

    public double MAtkBase { get; init; } = 10;
    public double MAtkPerZhili { get; init; } = 1.6;

    public double PDefBase { get; init; } = 10;
    public double PDefPerTongshuai { get; init; } = 0.9;
    public double PDefPerWuli { get; init; } = 0.5;

    public double MDefBase { get; init; } = 10;
    public double MDefPerZhili { get; init; } = 0.7;
    public double MDefPerYizhi { get; init; } = 0.6;

    public double SpdBase { get; init; } = 12;
    public double SpdPerWuli { get; init; } = 0.12;
    public double SpdPerYizhi { get; init; } = 0.10;

    // ---- 小兵领导加成（05 第 3 节）----
    public double UnitTongshuaiDivisor { get; init; } = 500;
    public double UnitDefTongshuaiFactor { get; init; } = 0.8;

    // ---- 士气（05 第 6 节）----
    public double MoraleMulBase { get; init; } = 0.6;
    public double MoraleMulSpan { get; init; } = 0.4;

    // ---- 伤害（05 第 5 节）----
    public double CounterFactor { get; init; } = 0.6;
    public double DamageRandMin { get; init; } = 0.9;
    public double DamageRandMax { get; init; } = 1.1;

    // ---- 俘获（05 第 8 节）----
    public double CaptureBase { get; init; } = 0.35;
    public double CapturePerSurround { get; init; } = 0.10;
    public double CaptureFleeingPenalty { get; init; } = -0.25;

    // ---- 招降（05 第 9 节）----
    public double PersuadeBase { get; init; } = 0.10;
    public double PersuadePerDetainMonth { get; init; } = 0.03;

    // ---- 战斗 ----
    public int BattleMaxRounds { get; init; } = 30;

    /// <summary>战场每回合每单位粮草消耗（05 第 7 节）。</summary>
    public int BattleFoodPerUnit { get; init; } = 2;

    public int BattleStarvationMoraleLoss { get; init; } = 15;
}

namespace MauiApp.Game.Battle;

/// <summary>
/// 一个单位本回合的完整行动：可选移动 + 一个主行动（攻击或待机）。
/// 对应 04-battle.md 第 5 节"移动 -> 行动"。
/// </summary>
public sealed class UnitTurn
{
    /// <summary>移动目标格；null 表示不移动。</summary>
    public (int Col, int Row)? MoveTo { get; set; }

    /// <summary>攻击目标单位 id；null 表示不攻击（待机）。</summary>
    public int? AttackTargetId { get; set; }

    /// <summary>技能伤害倍率（默认 1.0）。</summary>
    public double SkillMultiplier { get; set; } = 1.0;

    /// <summary>是否在本调用后结束单位回合。</summary>
    public bool EndTurn { get; set; } = true;

    /// <summary>撤退离场（须站在己方入场边缘）。</summary>
    public bool Retreat { get; set; }

    public static UnitTurn Wait() => new() { EndTurn = true };
    public static UnitTurn RetreatFromBattle() => new() { Retreat = true, EndTurn = true };
    public static UnitTurn MoveOnly(int col, int row) => new() { MoveTo = (col, row), EndTurn = true };
    /// <summary>仅移动，不结束回合（火焰纹章式玩家阶段）。</summary>
    public static UnitTurn MoveOnlyPending(int col, int row) => new() { MoveTo = (col, row), EndTurn = false };
    public static UnitTurn Attack(int targetId, double skillMul = 1.0) =>
        new() { AttackTargetId = targetId, SkillMultiplier = skillMul, EndTurn = true };
    public static UnitTurn MoveAndAttack(int col, int row, int targetId, double skillMul = 1.0) =>
        new() { MoveTo = (col, row), AttackTargetId = targetId, SkillMultiplier = skillMul, EndTurn = true };
    public static UnitTurn SkillAttack(int targetId, double skillMul = 1.5) => Attack(targetId, skillMul);
}

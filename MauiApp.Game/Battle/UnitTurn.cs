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

    public static UnitTurn Wait() => new();
    public static UnitTurn MoveOnly(int col, int row) => new() { MoveTo = (col, row) };
    public static UnitTurn Attack(int targetId) => new() { AttackTargetId = targetId };
    public static UnitTurn MoveAndAttack(int col, int row, int targetId) =>
        new() { MoveTo = (col, row), AttackTargetId = targetId };
}

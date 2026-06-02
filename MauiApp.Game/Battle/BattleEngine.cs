namespace MauiApp.Game.Battle;

/// <summary>
/// 战斗引擎骨架：方格战场、按速度行动序、移动/攻击/技能/特技、士气、携粮、30 回合上限。
/// 详见设计文档 04-battle.md，逻辑在 M4 里程碑实现。
/// 加速能力：FastResolveTurn()（整回合自动结算）、SkipToNextPlayerDecision()（快进到我方单位）。
/// </summary>
public sealed class BattleEngine
{
    // M4 实现：StartTurn()、Execute(action)、FastResolveTurn()、SkipToNextPlayerDecision()、IsFinished()。
}

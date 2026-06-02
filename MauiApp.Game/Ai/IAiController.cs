using MauiApp.Game.Battle;

namespace MauiApp.Game.Ai;

/// <summary>
/// 人机决策接口：为当前行动单位产出一个完整回合行动（移动 + 主行动）。
/// 难度分级见 04-battle.md 第 11 节。大地图回合决策（DecideWorldOrders）将在后续接入。
/// </summary>
public interface IAiController
{
    UnitTurn DecideTurn(BattleEngine engine, BattleUnit unit);
}

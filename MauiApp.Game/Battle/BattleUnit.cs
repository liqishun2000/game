using MauiApp.Game.Model;

namespace MauiApp.Game.Battle;

/// <summary>战斗的两个阵营。</summary>
public enum BattleSide
{
    Attacker,
    Defender,
}

/// <summary>战场上的一个作战单位（武将或小兵）。</summary>
public sealed class BattleUnit
{
    public int Id { get; set; }
    public BattleSide Side { get; set; }
    public string FactionId { get; set; } = "";
    public string Name { get; set; } = "";

    public bool IsGeneral { get; set; }

    /// <summary>武将单位对应的武将模板 id（小兵为 null）。</summary>
    public string? GeneralTemplateId { get; set; }

    /// <summary>当前装备 id（用于掉落判定）。</summary>
    public string? EquipmentId { get; set; }

    /// <summary>装备是否可掉落（唯一且 droppable）。</summary>
    public bool EquipmentDroppable { get; set; }

    /// <summary>大地图魅力/意志（仅武将有意义，用于俘获/招降判定）。</summary>
    public int Meili { get; set; }
    public int Yizhi { get; set; }

    /// <summary>小兵单位的世界态实例 id（用于战后回写；武将为 null）。</summary>
    public int? WorldUnitId { get; set; }

    public BattleStats Stats { get; set; } = new();
    public int MaxHp { get; set; }
    public int CurHp { get; set; }
    public int Morale { get; set; } = 100;
    public int Move { get; set; }

    public int Col { get; set; }
    public int Row { get; set; }

    /// <summary>本回合剩余行动次数（特性可>1）。</summary>
    public int ActionsLeft { get; set; }
    public bool HasMovedThisTurn { get; set; }

    public List<string> Traits { get; set; } = new();

    /// <summary>目标价值（AI 集火权重）：武将 > 特殊兵 > 普通兵。</summary>
    public int ThreatValue { get; set; } = 10;

    /// <summary>逃跑/撤退状态（影响被俘概率，05 第 8 节）。</summary>
    public bool IsFleeing { get; set; }

    public bool IsAlive => CurHp > 0;
}

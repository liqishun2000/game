namespace MauiApp.Game.Model;

/// <summary>
/// 战场六维：生命、物功、魔功、物防、魔防、速度。
/// 同时复用为装备的属性修正（statMods，未设置项为 0）。
/// </summary>
public sealed class BattleStats
{
    public int Hp { get; set; }
    public int PAtk { get; set; }
    public int MAtk { get; set; }
    public int PDef { get; set; }
    public int MDef { get; set; }
    public int Spd { get; set; }

    public BattleStats Clone() => new()
    {
        Hp = Hp, PAtk = PAtk, MAtk = MAtk, PDef = PDef, MDef = MDef, Spd = Spd,
    };
}

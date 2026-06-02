namespace MauiApp.Game.Model;

/// <summary>资源数量（金钱/粮食/科技点），用于成本与库存。</summary>
public sealed class Cost
{
    public int Gold { get; set; }
    public int Food { get; set; }
    public int TechPoints { get; set; }
}

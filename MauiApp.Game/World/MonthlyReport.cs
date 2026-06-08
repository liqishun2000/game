namespace MauiApp.Game.World;

/// <summary>单个势力的月度结算摘要。</summary>
public sealed class FactionMonthSummary
{
    public string FactionId { get; set; } = "";
    public int GoldGained { get; set; }
    public int FoodProduced { get; set; }
    public int FoodUpkeep { get; set; }
    public int TechGained { get; set; }
    public int Deserters { get; set; }
    public bool Starving => Deserters > 0;
}

/// <summary>一次月度推进的整体结算报告。</summary>
public sealed class MonthlyReport
{
    public int Month { get; set; }
    public List<FactionMonthSummary> Factions { get; } = new();
    public List<string> CompletedBuildings { get; } = new();
    /// <summary>AI 势力本月行动摘要（供 UI 展示）。</summary>
    public List<string> AiActions { get; } = new();
}

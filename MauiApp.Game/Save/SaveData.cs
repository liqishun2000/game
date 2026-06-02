namespace MauiApp.Game.Save;

/// <summary>
/// 存档模型骨架：序列化 GameState（随机种子、回合、势力资源/地盘/部队/监狱）。
/// 详见设计文档 01-architecture.md 第 6 节，实现在 M8 里程碑。
/// </summary>
public sealed class SaveData
{
    public int Seed { get; set; }
    public int Month { get; set; }
}

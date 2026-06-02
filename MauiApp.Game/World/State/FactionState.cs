using MauiApp.Game.Model;

namespace MauiApp.Game.World.State;

/// <summary>局内势力运行态。</summary>
public sealed class FactionState
{
    public string Id { get; set; } = "";
    public FactionDef Def { get; set; } = null!;

    public int Gold { get; set; }
    public int Food { get; set; }
    public int TechPoints { get; set; }

    public HashSet<string> ResearchedTechIds { get; } = new();

    /// <summary>被俘获的敌方武将（监狱）。</summary>
    public List<GeneralInstance> Prison { get; } = new();

    public FactionKind Kind => Def.Kind;
}

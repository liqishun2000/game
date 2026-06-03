using MauiApp.Game.App;
using MauiApp.Game.Model;

namespace MauiApp.Tutorial;

/// <summary>一条关卡目标。</summary>
public sealed class Objective
{
    public required string Text { get; init; }
    public bool Done { get; set; }
}

/// <summary>
/// 关卡目标追踪（常驻面板用）。针对 <c>v1_countryside</c>「乡野初阵」给出分步目标；
/// 其他地图回退为"消灭所有敌对诸侯"。纯 UI 侧逻辑，不改引擎。
/// </summary>
public sealed class LevelObjectives
{
    private readonly string _mapId;
    private bool _recruited;

    public List<Objective> Items { get; } = new();

    public LevelObjectives(string mapId)
    {
        _mapId = mapId;
        if (mapId == "v1_countryside")
        {
            Items.Add(new Objective { Text = "招募士兵，壮大队伍" });
            Items.Add(new Objective { Text = "攻占贼寨「北乡」" });
            Items.Add(new Objective { Text = "击败「敌寨」诸侯，平定乡野" });
        }
        else
        {
            Items.Add(new Objective { Text = "消灭所有敌对诸侯" });
        }
    }

    public bool AllDone => Items.Count > 0 && Items.All(o => o.Done);

    public void MarkRecruited() => _recruited = true;

    /// <summary>根据当前局面刷新完成状态。</summary>
    public void Evaluate(GameSession s)
    {
        if (_mapId == "v1_countryside")
        {
            Items[0].Done = _recruited;
            Items[1].Done = OwnedByPlayer(s, "n_c1_t");
            Items[2].Done = !AnyAiLordAlive(s);
        }
        else
        {
            Items[0].Done = !AnyAiLordAlive(s);
        }
    }

    private static bool OwnedByPlayer(GameSession s, string tileId) =>
        s.State.Tiles.TryGetValue(tileId, out var t) && t.OwnerFactionId == s.PlayerFactionId;

    private static bool AnyAiLordAlive(GameSession s) =>
        s.State.Factions.Values.Any(f => f.Kind == FactionKind.Ai && s.State.IsAlive(f.Id));
}

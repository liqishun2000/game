using MauiApp.Game.Battle;
using MauiApp.Game.Content;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.App;

/// <summary>
/// 单局会话外观：聚合大地图引擎、战争编排、监狱、AI 世界回合，给 UI 一个简洁入口。
/// 对应 M8 闭环联调。
/// </summary>
public sealed class GameSession
{
    public GameState State { get; }
    public WorldEngine World { get; }
    public WarService War { get; }
    public PrisonService Prison { get; }
    public IRandomSource Rng { get; }
    public AiDifficulty Difficulty { get; }

    public string PlayerFactionId { get; }

    public GameSession(GameState state, IRandomSource rng, AiDifficulty difficulty = AiDifficulty.Normal)
    {
        State = state;
        Rng = rng;
        Difficulty = difficulty;
        World = new WorldEngine(state, rng);
        War = new WarService(state, rng);
        Prison = new PrisonService(state, rng);
        PlayerFactionId = state.Factions.Values.First(f => f.Kind == FactionKind.Player).Id;
    }

    public static GameSession Start(ContentDatabase db, string mapId, int seed, AiDifficulty difficulty = AiDifficulty.Normal)
    {
        var state = GameStateFactory.CreateNewGame(db, mapId, seed);
        return new GameSession(state, new DeterministicRandom(seed), difficulty);
    }

    public FactionState PlayerFaction => State.Factions[PlayerFactionId];

    /// <summary>玩家发起进攻，返回待进行的战斗（UI 交互后调用 FinishBattle）。</summary>
    public PendingBattle StartPlayerAttack(string attackerTileId, string defenderTileId)
    {
        var atk = State.Tiles[attackerTileId];
        var pending = War.CreateBattle(
            attackerTileId, defenderTileId,
            atk.Generals.Select(g => g.TemplateId),
            atk.Units.Select(u => u.Id));

        pending.Engine.SetController(BattleSide.Defender, new Ai.BattleAi(Difficulty));
        pending.Engine.Start();
        return pending;
    }

    public void FinishBattle(PendingBattle pending) => War.ApplyResult(pending);

    /// <summary>结束玩家回合：AI 行动 -> 月结算。</summary>
    public MonthlyReport EndMonth()
    {
        RunAiTurn();
        return World.AdvanceMonth();
    }

    /// <summary>玩家可进攻的目标（相邻且非己方）。</summary>
    public IEnumerable<TileState> AttackTargets(string tileId)
    {
        var tile = State.Tiles[tileId];
        if (tile.OwnerFactionId != PlayerFactionId) yield break;
        bool hasForce = tile.Generals.Count > 0 || tile.Units.Count > 0;
        if (!hasForce) yield break;

        foreach (var id in tile.Adjacent)
        {
            var t = State.Tiles[id];
            if (t.OwnerFactionId != PlayerFactionId)
                yield return t;
        }
    }

    // ---- 简易世界 AI：招兵 + 对弱邻发起进攻 ----
    private void RunAiTurn()
    {
        foreach (var faction in State.Factions.Values)
        {
            if (faction.Kind is FactionKind.Player or FactionKind.Rebel) continue;
            AiRecruit(faction);
            AiAttack(faction);
        }
    }

    private void AiRecruit(FactionState faction)
    {
        string? basic = faction.Def.RecruitableUnitIds.FirstOrDefault(id =>
            State.Content.Units.TryGetValue(id, out var u) && !u.IsSpecial);
        if (basic is null) return;

        foreach (var tile in State.TilesOf(faction.Id).Where(t => !t.IsRebelFixed && t.Generals.Count > 0))
            World.Recruit(faction.Id, tile.Id, basic, 2);
    }

    private void AiAttack(FactionState faction)
    {
        foreach (var tile in State.TilesOf(faction.Id).ToList())
        {
            if (tile.IsRebelFixed) continue;
            if (Power(tile) <= 0) continue;

            foreach (var adjId in tile.Adjacent)
            {
                var target = State.Tiles[adjId];
                if (target.OwnerFactionId == faction.Id) continue;
                if (Power(tile) <= Power(target) * 1.3) continue;

                var pending = War.CreateBattle(
                    tile.Id, target.Id,
                    tile.Generals.Select(g => g.TemplateId),
                    tile.Units.Select(u => u.Id));
                pending.Engine.SetController(BattleSide.Attacker, new Ai.BattleAi(Difficulty));
                pending.Engine.SetController(BattleSide.Defender, new Ai.BattleAi(Difficulty));
                pending.Engine.Start();
                pending.Engine.FastResolveAll();
                War.ApplyResult(pending);
                break; // 每地块每回合至多一次进攻
            }
        }
    }

    private static double Power(TileState tile) =>
        tile.Generals.Sum(g => g.Template.MapStats.Wuli + g.Template.MapStats.Tongshuai) + tile.Units.Count * 40;
}

using MauiApp.Game.Ai;
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
    private readonly WorldAi _worldAi = new();
    private readonly Queue<PendingBattle> _pendingPlayerBattles = new();

    public GameState State { get; }
    public WorldEngine World { get; }
    public WarService War { get; }
    public PrisonService Prison { get; }
    public EquipService Equip { get; }
    public IRandomSource Rng { get; }
    public AiDifficulty Difficulty { get; }

    public string PlayerFactionId { get; }

    public GameSession(GameState state, IRandomSource rng, AiDifficulty? difficulty = null)
    {
        State = state;
        Rng = rng;
        Difficulty = difficulty ?? state.Difficulty;
        state.Difficulty = Difficulty;
        World = new WorldEngine(state, rng);
        War = new WarService(state, rng);
        Prison = new PrisonService(state, rng);
        Equip = new EquipService(state);
        PlayerFactionId = state.Factions.Values.First(f => f.Kind == FactionKind.Player).Id;
    }

    public static GameSession Start(ContentDatabase db, string mapId, int seed, AiDifficulty difficulty = AiDifficulty.Normal)
    {
        var state = GameStateFactory.CreateNewGame(db, mapId, seed);
        state.Difficulty = difficulty;
        return new GameSession(state, new DeterministicRandom(seed), difficulty);
    }

    public FactionState PlayerFaction => State.Factions[PlayerFactionId];

    /// <summary>玩家发起进攻，返回待进行的战斗（UI 交互后调用 FinishBattle）。</summary>
    public PendingBattle StartPlayerAttack(string attackerTileId, string defenderTileId, ExpeditionSetup setup)
    {
        var validation = ExpeditionPlanner.Validate(State, attackerTileId, setup);
        if (!validation.Success)
            throw new InvalidOperationException(validation.Message);

        var atk = State.Tiles[attackerTileId];
        var pending = War.CreateBattle(
            attackerTileId, defenderTileId,
            setup.GeneralTemplateIds,
            setup.UnitWorldIds,
            setup.CarriedFood,
            awaitDeployment: true);

        War.CommitBattleFood(pending);

        foreach (var id in setup.GeneralTemplateIds)
        {
            var g = atk.Generals.First(x => x.TemplateId == id);
            g.ActedThisMonth = true;
        }

        return pending;
    }

    public void FinishBattle(PendingBattle pending) => War.ApplyResult(pending);

    /// <summary>为交互式战斗配置 AI 并开局（玩家方由 UI 操控）。</summary>
    public void BeginInteractiveBattle(PendingBattle pending)
    {
        var aiSide = pending.Engine.State.PlayerSide == BattleSide.Attacker
            ? BattleSide.Defender
            : BattleSide.Attacker;
        pending.Engine.SetController(aiSide, new BattleAi(Difficulty));
        pending.Engine.Start();
    }

    /// <summary>为交互式战斗配置 AI 并开局（玩家方由 UI 操控）。</summary>
    [Obsolete("Use BeginInteractiveBattle after optional deployment.")]
    public void PrepareInteractiveBattle(PendingBattle pending) => BeginInteractiveBattle(pending);

    internal void EnqueuePlayerBattle(PendingBattle pending) => _pendingPlayerBattles.Enqueue(pending);

    public bool HasPendingPlayerBattles => _pendingPlayerBattles.Count > 0;

    public bool TryTakeNextPlayerBattle(out PendingBattle? pending)
    {
        if (_pendingPlayerBattles.Count == 0)
        {
            pending = null;
            return false;
        }

        pending = _pendingPlayerBattles.Dequeue();
        return true;
    }

    /// <summary>结束玩家回合：AI 行动 -> 月结算。</summary>
    public MonthlyReport EndMonth()
    {
        _worldAi.RunTurn(State, War, World, Difficulty, Rng, EnqueuePlayerBattle);
        var report = World.AdvanceMonth();
        report.AiActions.AddRange(_worldAi.ActionLogs);
        return report;
    }

    /// <summary>玩家可进攻的目标（相邻且非己方，且有未行动武将）。</summary>
    public IEnumerable<TileState> AttackTargets(string tileId)
    {
        var tile = State.Tiles[tileId];
        if (tile.OwnerFactionId != PlayerFactionId) yield break;
        if (!tile.Generals.Any(g => !g.ActedThisMonth)) yield break;

        foreach (var id in tile.Adjacent)
        {
            var t = State.Tiles[id];
            if (t.OwnerFactionId != PlayerFactionId)
                yield return t;
        }
    }
}

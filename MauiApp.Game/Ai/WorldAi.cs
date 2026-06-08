using MauiApp.Game.Battle;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Ai;

/// <summary>大地图 AI：招兵、扩张、对玩家/叛军发起进攻；输出行动日志供 UI 展示。</summary>
public sealed class WorldAi
{
    public List<string> ActionLogs { get; } = new();

    public void RunTurn(GameState state, WarService war, WorldEngine world, AiDifficulty difficulty, IRandomSource rng,
        Action<PendingBattle>? enqueuePlayerBattle = null)
    {
        ActionLogs.Clear();
        int maxAttacks = difficulty switch
        {
            AiDifficulty.Easy => 1,
            AiDifficulty.Hard => 2,
            _ => 1,
        };

        foreach (var faction in state.Factions.Values.Where(f => f.Kind == FactionKind.Ai))
        {
            AiRecruit(state, world, faction, difficulty);
            AiAttack(state, war, faction, difficulty, maxAttacks, enqueuePlayerBattle);
        }
    }

    private void AiRecruit(GameState state, WorldEngine world, FactionState faction, AiDifficulty difficulty)
    {
        string? basic = faction.Def.RecruitableUnitIds.FirstOrDefault(id =>
            state.Content.Units.TryGetValue(id, out var u) && !u.IsSpecial);
        if (basic is null) return;

        int batch = difficulty switch
        {
            AiDifficulty.Easy => 1,
            AiDifficulty.Hard => 4,
            _ => 2,
        };

        if (faction.Food < batch * 30) return;

        var tiles = state.TilesOf(faction.Id).Where(t => !t.IsRebelFixed).ToList();
        if (tiles.Count == 0) return;

        // 优先有武将或出生点的地块
        var target = tiles.OrderByDescending(t => t.Generals.Count)
            .ThenByDescending(t => state.Content.Maps[state.MapId].Spawns.GetValueOrDefault(faction.Id) == t.Id)
            .First();

        var r = world.Recruit(faction.Id, target.Id, basic, batch);
        if (r.Success)
            ActionLogs.Add($"{faction.Def.Name} 在 {target.Name} 招募 {batch} 名士兵");
    }

    private void AiAttack(GameState state, WarService war, FactionState faction, AiDifficulty difficulty, int maxAttacks,
        Action<PendingBattle>? enqueuePlayerBattle)
    {
        int attacks = 0;
        var tiles = state.TilesOf(faction.Id).Where(t => !t.IsRebelFixed && t.Generals.Count > 0).ToList();
        if (tiles.Count == 0) return;

        var candidates = new List<(TileState From, TileState Target, double Score)>();

        foreach (var from in tiles)
        {
            if (from.Generals.Count == 0) continue;
            double fromPower = TilePower(from);

            foreach (var adjId in from.Adjacent)
            {
                var target = state.Tiles[adjId];
                if (target.OwnerFactionId == faction.Id) continue;
                if (target.Generals.Count == 0 && target.Units.Count == 0 && !target.IsRebelFixed) continue;

                double targetPower = TilePower(target);
                double score = ScoreTarget(state, from, target, fromPower, targetPower, difficulty);
                if (score > 0)
                    candidates.Add((from, target, score));
            }
        }

        foreach (var (from, target, _) in candidates.OrderByDescending(c => c.Score))
        {
            if (attacks >= maxAttacks) break;

            var availableGens = from.Generals.Where(g => !g.ActedThisMonth).ToList();
            if (availableGens.Count == 0) continue;

            string targetLabel = target.Name;
            string ownerLabel = target.IsRebelFixed ? "反贼" :
                state.Factions.TryGetValue(target.OwnerFactionId, out var tf) ? tf.Def.Name : "中立";

            try
            {
                var pending = war.CreateBattle(
                    from.Id, target.Id,
                    availableGens.Select(g => g.TemplateId),
                    from.Units.Select(u => u.Id));

                foreach (var g in availableGens)
                    g.ActedThisMonth = true;

                bool playerDefender = state.Factions.TryGetValue(target.OwnerFactionId, out var defOwner)
                    && defOwner.Kind == FactionKind.Player;

                if (playerDefender && enqueuePlayerBattle is not null)
                {
                    war.CommitBattleFood(pending);
                    pending.Engine.SetController(BattleSide.Attacker, new BattleAi(difficulty));
                    pending.Engine.Start();
                    enqueuePlayerBattle(pending);
                    ActionLogs.Add($"{faction.Def.Name} 自 {from.Name} 进攻 {targetLabel}（{ownerLabel}）——请迎战！");
                }
                else
                {
                    war.CommitBattleFood(pending);
                    pending.Engine.SetController(BattleSide.Attacker, new BattleAi(difficulty));
                    pending.Engine.SetController(BattleSide.Defender, new BattleAi(difficulty));
                    pending.Engine.Start();
                    pending.Engine.FastResolveAll();
                    war.ApplyResult(pending);
                    ActionLogs.Add($"{faction.Def.Name} 自 {from.Name} 进攻 {targetLabel}（{ownerLabel}）");
                }

                attacks++;
            }
            catch
            {
                // 校验失败则跳过
            }
        }
    }

    private static double ScoreTarget(GameState state, TileState from, TileState target,
        double fromPower, double targetPower, AiDifficulty difficulty)
    {
        if (from.Generals.Count == 0) return -1;

        double ratio = fromPower / Math.Max(1, targetPower);
        double minRatio = difficulty switch
        {
            AiDifficulty.Easy => 1.15,
            AiDifficulty.Hard => 0.75,
            _ => 0.95,
        };
        if (ratio < minRatio) return -1;

        double score = ratio * 10;

        if (state.Factions.TryGetValue(target.OwnerFactionId, out var owner) && owner.Kind == FactionKind.Player)
            score += difficulty == AiDifficulty.Hard ? 40 : 25;

        if (target.IsRebelFixed)
            score += 15;

        // 优先打弱邻
        score += Math.Max(0, 20 - targetPower / 40);

        return score;
    }

    private static double TilePower(TileState tile) =>
        tile.Generals.Sum(g => g.Template.MapStats.Wuli + g.Template.MapStats.Tongshuai)
        + tile.Units.Count * 40;
}

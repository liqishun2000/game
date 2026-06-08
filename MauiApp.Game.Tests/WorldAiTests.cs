using MauiApp.Game.Ai;
using MauiApp.Game.App;
using MauiApp.Game.Battle;
using MauiApp.Game.Content;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Tests;

public class WorldAiTests
{
    private static ContentDatabase LoadDb()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "content");
        var result = new ContentLoader().LoadFromDirectory(root);
        Assert.True(result.Success, result.Validation.ToString());
        return result.Database!;
    }

    [Fact]
    public void Attack_Requires_General()
    {
        var db = LoadDb();
        var state = GameStateFactory.CreateNewGame(db, "v1_countryside", 42);
        var war = new WarService(state, new DeterministicRandom(1));

        var atk = state.Tiles["n_left1"];
        var unitsOnly = atk.Units.Select(u => u.Id);
        Assert.Throws<InvalidOperationException>(() =>
            war.CreateBattle(atk.Id, "n_c1_t", Array.Empty<string>(), unitsOnly));
    }

    [Fact]
    public void Ai_Attacks_Or_Recruits_Within_10_Months_When_Adjacent_To_Player()
    {
        var db = LoadDb();
        var session = GameSession.Start(db, "v1_countryside", 99, AiDifficulty.Normal);

        // 模拟玩家占中盘，AI 仍在敌寨且与玩家地块相邻
        var player = session.PlayerFactionId;
        session.State.Tiles["n_mid_m"].OwnerFactionId = player;
        session.State.Tiles["n_mid_m"].IsRebelFixed = false;
        session.State.Tiles["n_c3_t"].OwnerFactionId = player;
        session.State.Tiles["n_c3_t"].IsRebelFixed = false;

        bool anyAction = false;
        for (int i = 0; i < 10; i++)
        {
            var report = session.EndMonth();
            if (report.AiActions.Count > 0)
            {
                anyAction = true;
                break;
            }
        }

        Assert.True(anyAction, "AI 应在 10 个月内至少招兵或进攻一次");
    }

    [Fact]
    public void Ai_Attack_On_Player_Queues_Interactive_Battle()
    {
        var db = LoadDb();
        var session = GameSession.Start(db, "v1_countryside", 99, AiDifficulty.Normal);
        var player = session.PlayerFactionId;
        session.State.Tiles["n_mid_m"].OwnerFactionId = player;
        session.State.Tiles["n_mid_m"].IsRebelFixed = false;
        session.State.Tiles["n_c3_t"].OwnerFactionId = player;
        session.State.Tiles["n_c3_t"].IsRebelFixed = false;

        for (int i = 0; i < 15; i++)
        {
            var report = session.EndMonth();
            if (!session.HasPendingPlayerBattles) continue;

            Assert.True(session.TryTakeNextPlayerBattle(out var pending));
            Assert.NotNull(pending);
            Assert.Equal(BattleSide.Defender, pending!.Engine.State.PlayerSide);
            Assert.False(pending.Engine.Result.Finished);
            Assert.Contains(report.AiActions, log => log.Contains("请迎战"));
            return;
        }

        Assert.Fail("AI 在玩家扩张后应至少一次排队玩家防守战");
    }

    [Fact]
    public void PrepareInteractiveBattle_Does_Not_Auto_Finish()
    {
        var db = LoadDb();
        var session = GameSession.Start(db, "v1_countryside", 7);
        session.State.Tiles["n_c3_t"].OwnerFactionId = session.PlayerFactionId;
        session.State.Tiles["n_c3_t"].IsRebelFixed = false;

        var atk = session.State.Tiles["n_right1"];
        var pending = session.War.CreateBattle(
            atk.Id, "n_c3_t",
            atk.Generals.Select(g => g.TemplateId),
            atk.Units.Select(u => u.Id));

        session.BeginInteractiveBattle(pending);

        Assert.Equal(BattleSide.Defender, pending.Engine.State.PlayerSide);
        Assert.False(pending.Engine.Result.Finished);
    }

    [Fact]
    public void General_Cannot_Attack_Twice_Same_Month()
    {
        var db = LoadDb();
        var session = GameSession.Start(db, "v1_countryside", 7, AiDifficulty.Normal);
        var tile = session.State.Tiles["n_left1"];
        var guanyu = tile.Generals.First(g => g.TemplateId == "guanyu");

        var setup = ExpeditionSetup.AllFromTile(tile, carriedFood: 0);
        session.StartPlayerAttack("n_left1", "n_c1_t", setup);
        // 战斗未结束也消耗将令（简化：出征即标记）
        Assert.True(guanyu.ActedThisMonth);

        session.EndMonth();
        Assert.False(guanyu.ActedThisMonth);
    }
}

using MauiApp.Game.App;
using MauiApp.Game.Battle;
using MauiApp.Game.World;
using MauiApp.Rendering;
using MauiApp.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MauiApp.Pages;

public partial class BattlePage : ContentPage
{
    private readonly GameSession _session;
    private readonly PendingBattle _pending;
    private readonly BattleEngine _engine;
    private readonly BattleRenderer _renderer = new();
    private readonly AudioService _audio;

    private AnimationClock _clock = default!;
    private BattleAnimator _anim = default!;

    private BattleUnit? _current;
    private HashSet<(int Col, int Row)> _reachable = new();
    private HashSet<int> _attackable = new();
    private bool _finalized;
    private bool _busy;

    public BattlePage(GameSession session, PendingBattle pending)
    {
        InitializeComponent();
        _session = session;
        _pending = pending;
        _engine = pending.Engine;
        _audio = ServiceHelper.Get<AudioService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _clock = new AnimationClock(Dispatcher, () => BattleCanvas.InvalidateSurface());
        _anim = new BattleAnimator(_clock);

        await PixelFont.EnsureLoadedAsync();
        await _audio.PreloadAsync(new[]
        {
            AudioKeys.BgmBattle, AudioKeys.SfxMove, AudioKeys.SfxHit, AudioKeys.SfxArrow,
            AudioKeys.SfxDown, AudioKeys.SfxVictory, AudioKeys.SfxDefeat,
        });
        await _audio.PlayBgmAsync(AudioKeys.BgmBattle);

        if (!SettingsStore.IsTutorialDone("battle"))
            BattleCoach.IsVisible = true;

        await RunUntilPlayerAsync();
    }

    private void OnBattleCoachClose(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        BattleCoach.IsVisible = false;
        SettingsStore.SetTutorialDone("battle");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _clock?.Stop();
        _audio.StopBgm();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e) =>
        _renderer.Draw(e.Surface.Canvas, e.Info, _engine.State, _current, _reachable, _attackable,
            _anim?.Vfx ?? new BattleVfx(), _clock?.TimeSeconds ?? 0f);

    // ---------- 输入 ----------
    private async void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType == SKTouchAction.Pressed && _current is not null && !_busy)
        {
            var cell = _renderer.HitTest(_engine.State, e.Location.X, e.Location.Y);
            if (cell is { } c) await HandleTapAsync(c.Col, c.Row);
        }
        e.Handled = true;
    }

    private async Task HandleTapAsync(int col, int row)
    {
        if (_current is null || _busy) return;

        var target = _engine.State.UnitAt(col, row);

        if (target is not null && target.Side != _current.Side && _attackable.Contains(target.Id))
        {
            var cell = _reachable
                .Where(p => Manhattan(p.Col, p.Row, target.Col, target.Row) == 1)
                .OrderBy(p => Manhattan(p.Col, p.Row, _current.Col, _current.Row))
                .First();
            var turn = (cell.Col == _current.Col && cell.Row == _current.Row)
                ? UnitTurn.Attack(target.Id)
                : UnitTurn.MoveAndAttack(cell.Col, cell.Row, target.Id);
            await PlayPlayerTurnAsync(turn);
            return;
        }

        if (target is null && _reachable.Contains((col, row)))
            await PlayPlayerTurnAsync(UnitTurn.MoveOnly(col, row));
    }

    private async Task PlayPlayerTurnAsync(UnitTurn turn)
    {
        var actor = _current;
        if (actor is null) return;
        await PlayActionAsync(actor, turn);
        await RunUntilPlayerAsync();
    }

    private async void OnWaitClicked(object? sender, EventArgs e)
    {
        if (_current is not null && !_busy)
        {
            _audio.PlaySfx(AudioKeys.SfxMove);
            await PlayPlayerTurnAsync(UnitTurn.Wait());
        }
    }

    private async void OnFastToPlayerClicked(object? sender, EventArgs e) => await FastAsync(() => _engine.SkipToNextPlayerDecision());
    private async void OnFastRoundClicked(object? sender, EventArgs e) => await FastAsync(() => _engine.FastResolveTurn());
    private async void OnAutoClicked(object? sender, EventArgs e) => await FastAsync(() => _engine.FastResolveAll());

    private async Task FastAsync(Action bulk)
    {
        if (_busy) return;
        _busy = true;
        bulk();
        Recompute();
        BattleCanvas.InvalidateSurface();
        _busy = false;
        await MaybeFinalizeAsync();
        await RunUntilPlayerAsync();
    }

    // ---------- 演出驱动 ----------
    private async Task RunUntilPlayerAsync()
    {
        while (true)
        {
            if (_engine.IsFinished(out _)) break;
            var u = _engine.CurrentUnit();
            if (u is null) break;
            if (u.Side == _engine.State.PlayerSide) break;
            await PlayActionAsync(u, _engine.BuildAutoTurn(u));
        }

        Recompute();
        if (_clock is not null) _clock.AlwaysAnimate = _current is not null && _current.Side == _engine.State.PlayerSide;
        _clock?.Wake();
        BattleCanvas.InvalidateSurface();
        await MaybeFinalizeAsync();
    }

    /// <summary>把一次"瞬间结算"补放为动画：移动→冲刺→受击飘字/震屏→反击→阵亡。</summary>
    private async Task PlayActionAsync(BattleUnit actor, UnitTurn turn)
    {
        _busy = true;

        var hpBefore = _engine.State.Units.ToDictionary(u => u.Id, u => u.CurHp);
        var aliveBefore = _engine.State.Units.Where(u => u.IsAlive).Select(u => u.Id).ToHashSet();
        var from = (actor.Col, actor.Row);
        int capBefore = _engine.Result.Captured.Count;
        int killBefore = _engine.Result.KilledGenerals.Count;
        int escBefore = _engine.Result.EscapedGenerals.Count;

        bool ranged = actor.Stats.MAtk > actor.Stats.PAtk;

        _engine.ExecuteTurn(turn);

        var to = (actor.Col, actor.Row);
        if (to != from && actor.IsAlive)
        {
            _audio.PlaySfx(AudioKeys.SfxMove);
            await _anim.MoveAsync(actor.Id, from, to);
        }

        if (turn.AttackTargetId is { } targetId)
        {
            var target = _engine.State.GetUnit(targetId);
            if (target is not null)
            {
                await _anim.LungeAsync(actor.Id, target.Col - actor.Col, target.Row - actor.Row);

                int dmg = hpBefore.GetValueOrDefault(targetId) - target.CurHp;
                if (dmg > 0)
                {
                    bool crit = dmg > Math.Max(1, target.MaxHp / 2);
                    _anim.SpawnDamage(target.Col, target.Row, dmg, crit, target.Side == _engine.State.PlayerSide);
                    _anim.Flash(targetId);
                    _anim.Shake(crit ? 7f : 4f);
                    _audio.PlaySfx(ranged ? AudioKeys.SfxArrow : AudioKeys.SfxHit);
                }

                int counter = hpBefore.GetValueOrDefault(actor.Id) - actor.CurHp;
                if (counter > 0 && actor.IsAlive)
                {
                    _anim.SpawnDamage(actor.Col, actor.Row, counter, false, actor.Side == _engine.State.PlayerSide);
                    _anim.Flash(actor.Id);
                }

                await Task.Delay((int)(160 / Math.Max(0.1f, _anim.SpeedScale)));
            }
        }

        await PlayDeathsAsync(aliveBefore, capBefore, killBefore, escBefore);

        BattleCanvas.InvalidateSurface();
        _busy = false;
    }

    private async Task PlayDeathsAsync(HashSet<int> aliveBefore, int capBefore, int killBefore, int escBefore)
    {
        var newlyDead = _engine.State.Units
            .Where(u => !u.IsAlive && aliveBefore.Contains(u.Id))
            .ToList();
        if (newlyDead.Count == 0) return;

        _audio.PlaySfx(AudioKeys.SfxDown);

        // 武将结局飘字
        SpawnGeneralLabels(_engine.Result.Captured.Skip(capBefore).Select(c => c.GeneralTemplateId), "被俘", new SKColor(0xff, 0xd1, 0x40));
        SpawnGeneralLabels(_engine.Result.KilledGenerals.Skip(killBefore), "阵亡", new SKColor(0xff, 0x6a, 0x4a));
        SpawnGeneralLabels(_engine.Result.EscapedGenerals.Skip(escBefore), "突围", new SKColor(0x8a, 0xd0, 0xff));

        var dying = newlyDead.Select(u => new DyingUnit
        {
            Id = u.Id, Col = u.Col, Row = u.Row, Side = u.Side, IsGeneral = u.IsGeneral,
        }).ToList();
        await _anim.DieAsync(dying);
    }

    private void SpawnGeneralLabels(IEnumerable<string> templateIds, string label, SKColor color)
    {
        foreach (var tid in templateIds)
        {
            var unit = _engine.State.Units.FirstOrDefault(u => u.GeneralTemplateId == tid);
            if (unit is not null) _anim.SpawnLabel(unit.Col, unit.Row, label, color);
        }
    }

    // ---------- 状态/结算 ----------
    private void Recompute()
    {
        _current = _engine.CurrentUnit();
        if (_current is null || _current.Side != _engine.State.PlayerSide)
        {
            _reachable = new();
            _attackable = new();
            UpdateStatus();
            return;
        }

        _reachable = _engine.GetReachable(_current);
        _attackable = _engine.State.Units
            .Where(u => u.IsAlive && u.Side != _current.Side &&
                        _reachable.Any(p => Manhattan(p.Col, p.Row, u.Col, u.Row) == 1))
            .Select(u => u.Id)
            .ToHashSet();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int atk = _engine.State.AliveOf(BattleSide.Attacker).Count();
        int def = _engine.State.AliveOf(BattleSide.Defender).Count();
        string who = _current is null ? "—" : $"{_current.Name}（{(_current.Side == _engine.State.PlayerSide ? "我方" : "敌方")}）";
        StatusLabel.Text = $"第 {_engine.State.Round}/{_engine.State.MaxRounds} 回合   我方 {atk}  敌方 {def}   当前:{who}";
    }

    private async Task MaybeFinalizeAsync()
    {
        if (_finalized || !_engine.IsFinished(out var result)) return;
        _finalized = true;

        _session.FinishBattle(_pending);

        bool win = result.Outcome switch
        {
            BattleOutcome.AttackerWins => _engine.State.PlayerSide == BattleSide.Attacker,
            BattleOutcome.DefenderWins => _engine.State.PlayerSide == BattleSide.Defender,
            _ => false,
        };

        string title = result.Outcome == BattleOutcome.Timeout ? "平局" : win ? "胜利" : "战败";

        var parts = new List<string> { $"共 {result.Rounds} 回合" };
        if (result.Captured.Count > 0)
            parts.Add("俘获: " + string.Join("、", result.Captured.Select(c => Name(c.GeneralTemplateId))));
        if (result.Drops.Count > 0)
            parts.Add("缴获: " + string.Join("、", result.Drops.Select(d => Name(d.EquipmentId, equip: true))));
        if (result.KilledGenerals.Count > 0)
            parts.Add("斩将: " + string.Join("、", result.KilledGenerals.Select(g => Name(g))));

        _audio.PlaySfx(win ? AudioKeys.SfxVictory : AudioKeys.SfxDefeat);

        ResultTitle.Text = title;
        ResultTitle.TextColor = win ? (Color)Application.Current!.Resources["PixelGold"]
                                     : (Color)Application.Current!.Resources["PixelCrimson"];
        ResultBody.Text = string.Join("\n", parts);
        ResultOverlay.IsVisible = true;
        await Task.CompletedTask;
    }

    private async void OnResultConfirm(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxConfirm);
        await Navigation.PopAsync();
    }

    private string Name(string id, bool equip = false)
    {
        if (equip)
            return _session.State.Content.Equipment.TryGetValue(id, out var eq) ? eq.Name : id;
        return _session.State.Content.Generals.TryGetValue(id, out var g) ? g.Name : id;
    }

    private static int Manhattan(int c1, int r1, int c2, int r2) =>
        Math.Abs(c1 - c2) + Math.Abs(r1 - r2);
}

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
    private enum PlayerPhase { Move, Action }

    private readonly GameSession _session;
    private readonly PendingBattle _pending;
    private readonly BattleEngine _engine;
    private readonly BattleRenderer _renderer = new();
    private readonly AudioService _audio;
    private readonly MapCamera _camera = new();

    private AnimationClock _clock = default!;
    private BattleAnimator _anim = default!;

    private BattleUnit? _current;
    private HashSet<(int Col, int Row)> _reachable = new();
    private HashSet<int> _attackable = new();
    private PlayerPhase _phase = PlayerPhase.Move;
    private bool _selectingAttackTarget;
    private bool _selectingSkillTarget;
    private bool _finalized;
    private bool _busy;
    private bool _deployMode;
    private BattleUnit? _deploySelected;
    private float _canvasW, _canvasH;
    private bool _cameraInit;
    private float TopBarH => MathF.Min(54, _canvasH * 0.12f) + SafeInsets.Top;

    public BattlePage(GameSession session, PendingBattle pending)
    {
        InitializeComponent();
        _session = session;
        _pending = pending;
        _engine = pending.Engine;
        _deployMode = pending.AwaitDeployment && !pending.Engine.State.IsStarted;
        _audio = ServiceHelper.Get<AudioService>();
        MapCanvasGestures.Attach(BattleCanvas, OnBattlePan, OnBattleTap, OnBattlePinch);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        OrientationService.LockLandscape();
        ImmersiveService.Enable();
        _clock = new AnimationClock(Dispatcher, () => BattleCanvas.InvalidateSurface());
        _anim = new BattleAnimator(_clock);

        await PixelFont.EnsureLoadedAsync();
        await GfxAssets.EnsureBattleCoreAsync();
        var portraitIds = _engine.State.Units
            .Where(u => u.GeneralTemplateId is not null)
            .Select(u => u.GeneralTemplateId!);
        await GfxAssets.PreloadPortraitsAsync(portraitIds);
        await _audio.PreloadAsync(new[]
        {
            AudioKeys.BgmBattle, AudioKeys.SfxMove, AudioKeys.SfxHit, AudioKeys.SfxArrow,
            AudioKeys.SfxDown, AudioKeys.SfxVictory, AudioKeys.SfxDefeat,
        });
        await _audio.PlayBgmAsync(AudioKeys.BgmBattle);

        if (!SettingsStore.IsTutorialDone("battle"))
            BattleCoach.IsVisible = true;

        if (_deployMode)
        {
            DeployMenu.IsVisible = true;
            BattleTools.IsVisible = false;
            UpdateDeployStatus();
        }
        else
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

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _canvasW = e.Info.Width;
        _canvasH = e.Info.Height;
        _renderer.ComputeLayout(_engine.State, TopBarH);
        if (!_cameraInit)
        {
            float vh = MapViewportH;
            var bounds = BattleRenderer.ComputeUnitBounds(_engine.State, _renderer.CellSize);
            float mapW = _renderer.MapPixelWidth;
            float mapH = _renderer.MapPixelHeight;
            _camera.MinZoom = Math.Min(0.2f, Math.Min(_canvasW, vh) / Math.Max(mapW, mapH) * 0.6f);
            _camera.MaxZoom = BattleRenderer.ComputeMaxZoom(_canvasW, vh, _renderer.CellSize);
            _camera.FitToBounds(_canvasW, vh,
                bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                mapW, mapH, margin: 1.05f);
            // 开局至少放大到视口宽度约 18 格，避免默认过小
            float minStart = _canvasW / (18f * _renderer.CellSize);
            if (_camera.Zoom < minStart)
            {
                _camera.Zoom = Math.Min(minStart, _camera.MaxZoom);
                _camera.FocusOnBounds(_canvasW, vh,
                    bounds.Left, bounds.Top, bounds.Width, bounds.Height, mapW, mapH);
            }
            _cameraInit = true;
        }
        else
            _camera.Clamp(_canvasW, _canvasH - TopBarH, _renderer.MapPixelWidth, _renderer.MapPixelHeight);
        _renderer.Draw(e.Surface.Canvas, e.Info, _engine.State, _current, _reachable, _attackable,
            _anim?.Vfx ?? new BattleVfx(), _clock?.TimeSeconds ?? 0f, _camera, SafeInsets.Top);
    }

    private void OnBattlePan(float dx, float dy)
    {
        _camera.Pan(dx, dy);
        _renderer.ComputeLayout(_engine.State, TopBarH);
        _camera.Clamp(_canvasW, _canvasH - TopBarH, _renderer.MapPixelWidth, _renderer.MapPixelHeight);
        BattleCanvas.InvalidateSurface();
    }

    private float MapViewportH => _canvasH - TopBarH;

    private void ApplyBattleZoom(float factor, float anchorScreenX, float anchorScreenY)
    {
        if (_canvasW <= 0 || MapViewportH <= 0) return;
        _renderer.ComputeLayout(_engine.State, TopBarH);
        _camera.MaxZoom = BattleRenderer.ComputeMaxZoom(_canvasW, MapViewportH, _renderer.CellSize);
        _camera.ZoomAt(anchorScreenX, anchorScreenY, factor,
            _canvasW, MapViewportH, _renderer.MapPixelWidth, _renderer.MapPixelHeight);
        BattleCanvas.InvalidateSurface();
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        ApplyBattleZoom(1.35f, _canvasW / 2f, MapViewportH / 2f);
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        ApplyBattleZoom(1f / 1.35f, _canvasW / 2f, MapViewportH / 2f);
    }

    private void OnBattlePinch(float factor, float anchorX, float anchorY) =>
        ApplyBattleZoom(factor, anchorX, anchorY - TopBarH);

    private async void OnBattleTap(float x, float y)
    {
        if (_busy) return;
        var cell = _renderer.HitTest(_engine.State, x, y, _camera, TopBarH);
        if (cell is not { } c) return;

        if (_deployMode)
        {
            HandleDeployTap(c.Col, c.Row);
            return;
        }

        if (_current is null) return;
        await HandleTapAsync(c.Col, c.Row);
    }

    private void HandleDeployTap(int col, int row)
    {
        var side = _engine.State.PlayerSide;
        var unit = _engine.State.UnitAt(col, row);

        if (unit is not null && unit.Side == side)
        {
            _deploySelected = unit;
            _audio.PlaySfx(AudioKeys.SfxClick);
            UpdateDeployStatus();
            BattleCanvas.InvalidateSurface();
            return;
        }

        if (_deploySelected is null) return;

        if (_engine.TryDeployUnit(_deploySelected.Id, col, row))
        {
            _audio.PlaySfx(AudioKeys.SfxMove);
            _deploySelected = _engine.State.GetUnit(_deploySelected.Id);
            UpdateDeployStatus();
            BattleCanvas.InvalidateSurface();
        }
    }

    private void OnAutoDeployClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        BattleFactory.AutoDeploySide(_engine.State, _engine.State.PlayerSide);
        _deploySelected = null;
        UpdateDeployStatus();
        BattleCanvas.InvalidateSurface();
    }

    private async void OnStartBattleClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxConfirm);
        _deployMode = false;
        DeployMenu.IsVisible = false;
        BattleTools.IsVisible = true;
        _deploySelected = null;
        _session.BeginInteractiveBattle(_pending);
        await RunUntilPlayerAsync();
    }

    private void UpdateDeployStatus()
    {
        int food = _engine.State.SideFood.GetValueOrDefault(_engine.State.PlayerSide);
        string sel = _deploySelected is null ? "未选中" : _deploySelected.Name;
        StatusLabel.Text = $"布阵阶段  携带粮草 {food}  ·  选中：{sel}  ·  点单位再点出生区空格移动";
    }

    // ---------- 输入（火焰纹章式：移动 → 行动菜单） ----------
    private async Task HandleTapAsync(int col, int row)
    {
        if (_current is null || _busy) return;

        if (_phase == PlayerPhase.Move)
        {
            if (_reachable.Contains((col, row)))
            {
                if (col != _current.Col || row != _current.Row)
                    await PlayMoveAsync(col, row);
                EnterActionPhase();
                if (_engine.CanCurrentRetreat())
                    StatusLabel.Text += "  可撤退回城";
            }
            return;
        }

        // 行动阶段：选攻击目标
        if (_selectingAttackTarget || _selectingSkillTarget)
        {
            var target = _engine.State.UnitAt(col, row);
            if (target is not null && _attackable.Contains(target.Id))
            {
                double mul = _selectingSkillTarget ? 1.5 : 1.0;
                _selectingAttackTarget = _selectingSkillTarget = false;
                await PlayActionAsync(target.Id, mul);
                await RunUntilPlayerAsync();
            }
        }
    }

    private void EnterActionPhase()
    {
        _phase = PlayerPhase.Action;
        _reachable = new HashSet<(int, int)> { (_current!.Col, _current.Row) };
        _attackable = _engine.GetAttackable(_current);
        ActionMenu.IsVisible = true;
        AttackButton.IsEnabled = _attackable.Count > 0;
        SkillButton.IsEnabled = _attackable.Count > 0;
        RetreatButton.IsEnabled = _engine.CanCurrentRetreat();
        UpdateStatus();
        BattleCanvas.InvalidateSurface();
    }

    private async Task PlayMoveAsync(int col, int row)
    {
        _busy = true;
        var actor = _current!;
        var from = (actor.Col, actor.Row);
        _engine.ExecuteMove(col, row);
        _audio.PlaySfx(AudioKeys.SfxMove);
        await _anim.MoveAsync(actor.Id, from, (col, row));
        BattleCanvas.InvalidateSurface();
        _busy = false;
        if (_phase == PlayerPhase.Action)
            RetreatButton.IsEnabled = _engine.CanCurrentRetreat();
    }

    private async void OnRetreatClicked(object? sender, EventArgs e)
    {
        if (_phase != PlayerPhase.Action || _busy || !_engine.CanCurrentRetreat()) return;
        _audio.PlaySfx(AudioKeys.SfxConfirm);
        _selectingAttackTarget = _selectingSkillTarget = false;
        await PlayRetreatAsync();
        await RunUntilPlayerAsync();
    }

    private async Task PlayRetreatAsync()
    {
        _busy = true;
        var actor = _current;
        if (actor is null) { _busy = false; return; }

        var aliveBefore = _engine.State.Units.Where(u => u.IsAlive).Select(u => u.Id).ToHashSet();
        int escBefore = _engine.Result.EscapedGenerals.Count;
        int capBefore = _engine.Result.Captured.Count;
        int killBefore = _engine.Result.KilledGenerals.Count;
        _engine.ExecuteRetreat();
        _audio.PlaySfx(AudioKeys.SfxMove);
        StatusLabel.Text = $"{actor.Name} 撤离战场，返回出发城池";
        ActionMenu.IsVisible = false;
        _phase = PlayerPhase.Move;
        BattleCanvas.InvalidateSurface();
        await PlayDeathsAsync(aliveBefore, capBefore, killBefore, escBefore);
        _busy = false;
    }

    private async Task PlayActionAsync(int? attackTargetId, double skillMul = 1.0)
    {
        _busy = true;
        var actor = _current;
        if (actor is null) { _busy = false; return; }

        var hpBefore = _engine.State.Units.ToDictionary(u => u.Id, u => u.CurHp);
        var aliveBefore = _engine.State.Units.Where(u => u.IsAlive).Select(u => u.Id).ToHashSet();
        int capBefore = _engine.Result.Captured.Count;
        int killBefore = _engine.Result.KilledGenerals.Count;
        int escBefore = _engine.Result.EscapedGenerals.Count;

        bool ranged = actor.Stats.MAtk > actor.Stats.PAtk;
        _engine.ExecuteAction(attackTargetId, skillMul);

        if (attackTargetId is { } tid)
        {
            var target = _engine.State.GetUnit(tid);
            if (target is not null)
            {
                await _anim.LungeAsync(actor.Id, target.Col - actor.Col, target.Row - actor.Row);
                int dmg = hpBefore.GetValueOrDefault(tid) - target.CurHp;
                if (dmg > 0)
                {
                    bool crit = dmg > Math.Max(1, target.MaxHp / 2);
                    _anim.SpawnDamage(target.Col, target.Row, dmg, crit, target.Side == _engine.State.PlayerSide);
                    _anim.Flash(tid);
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
        ActionMenu.IsVisible = false;
        _phase = PlayerPhase.Move;
        BattleCanvas.InvalidateSurface();
        _busy = false;
    }

    private void OnAttackMenuClicked(object? sender, EventArgs e)
    {
        if (_phase != PlayerPhase.Action || _attackable.Count == 0) return;
        _audio.PlaySfx(AudioKeys.SfxClick);
        _selectingAttackTarget = true;
        _selectingSkillTarget = false;
        StatusLabel.Text = "请选择攻击目标（点选红圈敌人）";
    }

    private void OnSkillMenuClicked(object? sender, EventArgs e)
    {
        if (_phase != PlayerPhase.Action || _attackable.Count == 0) return;
        _audio.PlaySfx(AudioKeys.SfxClick);
        _selectingSkillTarget = true;
        _selectingAttackTarget = false;
        StatusLabel.Text = "猛击：请选择相邻敌人（1.5×伤害）";
    }

    private async void OnDetailMenuClicked(object? sender, EventArgs e)
    {
        if (_current is null) return;
        _audio.PlaySfx(AudioKeys.SfxClick);
        var u = _current;
        string equip = u.EquipmentId is not null && _session.State.Content.Equipment.TryGetValue(u.EquipmentId, out var eq)
            ? eq.Name : "无";
        await DisplayAlertAsync(u.Name,
            $"HP {u.CurHp}/{u.MaxHp}  士气 {u.Morale}\n" +
            $"物攻 {u.Stats.PAtk}  物防 {u.Stats.PDef}\n" +
            $"魔攻 {u.Stats.MAtk}  魔防 {u.Stats.MDef}\n" +
            $"速度 {u.Stats.Spd}  移动力 {u.Move}\n" +
            $"装备 {equip}", "确定");
    }

    private async void OnActionWaitClicked(object? sender, EventArgs e)
    {
        if (_phase != PlayerPhase.Action || _busy) return;
        _audio.PlaySfx(AudioKeys.SfxMove);
        _selectingAttackTarget = _selectingSkillTarget = false;
        await PlayActionAsync(null);
        await RunUntilPlayerAsync();
    }

    private async void OnFastToPlayerClicked(object? sender, EventArgs e) => await FastAsync(() => _engine.SkipToNextPlayerDecision());
    private async void OnFastRoundClicked(object? sender, EventArgs e) => await FastAsync(() => _engine.FastResolveTurn());
    private async void OnAutoClicked(object? sender, EventArgs e) => await FastAsync(() => _engine.FastResolveAll());

    private async Task FastAsync(Action bulk)
    {
        if (_busy) return;
        _busy = true;
        ActionMenu.IsVisible = false;
        _selectingAttackTarget = _selectingSkillTarget = false;
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
            await PlayAiTurnAsync(u, _engine.DecideAutoTurn(u));
        }

        Recompute();
        if (_clock is not null) _clock.AlwaysAnimate = _current is not null && _current.Side == _engine.State.PlayerSide;
        _clock?.Wake();
        BattleCanvas.InvalidateSurface();
        await MaybeFinalizeAsync();
    }

    private async Task PlayAiTurnAsync(BattleUnit actor, UnitTurn turn)
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

        if (turn.Retreat)
        {
            StatusLabel.Text = $"{actor.Name} 撤离战场";
            await PlayDeathsAsync(aliveBefore, capBefore, killBefore, escBefore);
            _busy = false;
            return;
        }

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

    private void Recompute()
    {
        _current = _engine.CurrentUnit();
        _phase = PlayerPhase.Move;
        _selectingAttackTarget = _selectingSkillTarget = false;
        ActionMenu.IsVisible = false;

        if (_current is null || _current.Side != _engine.State.PlayerSide)
        {
            _reachable = new();
            _attackable = new();
            UpdateStatus();
            return;
        }

        _reachable = _engine.GetReachable(_current);
        _attackable = new HashSet<int>();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int atk = _engine.State.AliveOf(BattleSide.Attacker).Count();
        int def = _engine.State.AliveOf(BattleSide.Defender).Count();
        string who = _current is null ? "—" : $"{_current.Name}（{(_current.Side == _engine.State.PlayerSide ? "我方" : "敌方")}）";
        string phase = _phase == PlayerPhase.Move ? "移动" : "行动";
        int food = _engine.State.SideFood.GetValueOrDefault(_engine.State.PlayerSide);
        int starve = _engine.State.StarvationRounds.GetValueOrDefault(_engine.State.PlayerSide);
        string foodLine = starve > 0 ? $"  断粮{starve}回合" : $"  粮草{food}";
        StatusLabel.Text = $"第 {_engine.State.Round}/{_engine.State.MaxRounds} 回合   我方 {atk}  敌方 {def}{foodLine}   当前:{who} [{phase}]";
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
}

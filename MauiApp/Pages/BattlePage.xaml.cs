using MauiApp.Game.App;
using MauiApp.Game.Battle;
using MauiApp.Game.World;
using MauiApp.Rendering;
using SkiaSharp.Views.Maui;

namespace MauiApp.Pages;

public partial class BattlePage : ContentPage
{
    private readonly GameSession _session;
    private readonly PendingBattle _pending;
    private readonly BattleEngine _engine;
    private readonly BattleRenderer _renderer = new();

    private BattleUnit? _current;
    private HashSet<(int Col, int Row)> _reachable = new();
    private HashSet<int> _attackable = new();
    private bool _finalized;

    public BattlePage(GameSession session, PendingBattle pending)
    {
        InitializeComponent();
        _session = session;
        _pending = pending;
        _engine = pending.Engine;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AdvanceToPlayer();
        Recompute();
        BattleCanvas.InvalidateSurface();
        await MaybeFinalizeAsync();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e) =>
        _renderer.Draw(e.Surface.Canvas, e.Info, _engine.State, _current, _reachable, _attackable);

    private async void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType == SKTouchAction.Pressed && _current is not null)
        {
            var cell = _renderer.HitTest(_engine.State, e.Location.X, e.Location.Y);
            if (cell is { } c) await HandleTapAsync(c.Col, c.Row);
        }

        e.Handled = true;
    }

    private async Task HandleTapAsync(int col, int row)
    {
        if (_current is null) return;

        var target = _engine.State.UnitAt(col, row);

        // 点敌方可攻击单位：就近移动并攻击
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

        // 点可达空格：移动
        if (target is null && _reachable.Contains((col, row)))
            await PlayPlayerTurnAsync(UnitTurn.MoveOnly(col, row));
    }

    private async Task PlayPlayerTurnAsync(UnitTurn turn)
    {
        _engine.ExecuteTurn(turn);
        AdvanceToPlayer();
        Recompute();
        BattleCanvas.InvalidateSurface();
        await MaybeFinalizeAsync();
    }

    private async void OnWaitClicked(object? sender, EventArgs e)
    {
        if (_current is not null) await PlayPlayerTurnAsync(UnitTurn.Wait());
    }

    private async void OnFastToPlayerClicked(object? sender, EventArgs e)
    {
        _engine.SkipToNextPlayerDecision();
        Recompute();
        BattleCanvas.InvalidateSurface();
        await MaybeFinalizeAsync();
    }

    private async void OnFastRoundClicked(object? sender, EventArgs e)
    {
        _engine.FastResolveTurn();
        AdvanceToPlayer();
        Recompute();
        BattleCanvas.InvalidateSurface();
        await MaybeFinalizeAsync();
    }

    private async void OnAutoClicked(object? sender, EventArgs e)
    {
        _engine.FastResolveAll();
        Recompute();
        BattleCanvas.InvalidateSurface();
        await MaybeFinalizeAsync();
    }

    private void AdvanceToPlayer()
    {
        if (!_engine.IsFinished(out _))
            _engine.SkipToNextPlayerDecision();
    }

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

        string outcome = result.Outcome switch
        {
            BattleOutcome.AttackerWins => _engine.State.PlayerSide == BattleSide.Attacker ? "我方胜利！" : "我方战败",
            BattleOutcome.DefenderWins => _engine.State.PlayerSide == BattleSide.Defender ? "我方守住！" : "我方战败",
            _ => "回合耗尽，平局",
        };

        var parts = new List<string> { outcome, $"共 {result.Rounds} 回合" };
        if (result.Captured.Count > 0)
            parts.Add("俘获: " + string.Join("、", result.Captured.Select(c => Name(c.GeneralTemplateId))));
        if (result.Drops.Count > 0)
            parts.Add("缴获: " + string.Join("、", result.Drops.Select(d => Name(d.EquipmentId, equip: true))));

        await DisplayAlertAsync("战斗结束", string.Join("\n", parts), "返回大地图");
        await Navigation.PopAsync();
    }

    private string Name(string id, bool equip = false)
    {
        if (equip)
            return _session.State.Content.Equipment.TryGetValue(id, out var e) ? e.Name : id;
        return _session.State.Content.Generals.TryGetValue(id, out var g) ? g.Name : id;
    }

    private static int Manhattan(int c1, int r1, int c2, int r2) =>
        Math.Abs(c1 - c2) + Math.Abs(r1 - r2);
}

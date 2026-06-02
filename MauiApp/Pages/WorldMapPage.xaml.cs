using MauiApp.Game.App;
using MauiApp.Game.Model;
using MauiApp.Game.World.State;
using MauiApp.Rendering;
using MauiApp.Services;
using SkiaSharp.Views.Maui;

namespace MauiApp.Pages;

public partial class WorldMapPage : ContentPage
{
    private readonly GameSession _session;
    private readonly WorldMapRenderer _renderer = new();
    private MapLayout _layout = new();
    private string? _selectedTileId;
    private HashSet<string> _attackTargets = new();

    public WorldMapPage(GameSession session)
    {
        InitializeComponent();
        _session = session;
        RefreshHud();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshHud();
        UpdateSelectionUi();
        MapCanvas.InvalidateSurface();
        CheckGameOver();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _layout = MapLayout.Build(_session.State, e.Info.Width, e.Info.Height);
        _renderer.Draw(e.Surface.Canvas, e.Info, _session.State, _layout, _selectedTileId, _attackTargets);
    }

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType == SKTouchAction.Pressed)
        {
            var hit = _layout.HitTest(e.Location.X, e.Location.Y);
            if (hit is not null)
            {
                _selectedTileId = hit;
                UpdateSelectionUi();
                MapCanvas.InvalidateSurface();
            }
        }

        e.Handled = true;
    }

    private void RefreshHud()
    {
        var f = _session.PlayerFaction;
        HudLabel.Text = $"第 {_session.State.Month} 月   金 {f.Gold}   粮 {f.Food}   科技 {f.TechPoints}   监狱 {f.Prison.Count}";
    }

    private void UpdateSelectionUi()
    {
        if (_selectedTileId is null || !_session.State.Tiles.TryGetValue(_selectedTileId, out var tile))
        {
            TileLabel.Text = "点击地图选择地盘";
            BuildButton.IsEnabled = RecruitButton.IsEnabled = AttackButton.IsEnabled = false;
            _attackTargets = new();
            return;
        }

        bool mine = tile.OwnerFactionId == _session.PlayerFactionId && !tile.IsRebelFixed;
        var gens = string.Join("、", tile.Generals.Select(g => g.Template.Name));
        TileLabel.Text =
            $"{tile.Name}（{TypeName(tile.Type)}）  归属:{OwnerName(tile)}\n" +
            $"建筑:{tile.Buildings.Count}  武将:{(gens.Length == 0 ? "无" : gens)}  小兵:{tile.Units.Count}";

        _attackTargets = _session.AttackTargets(_selectedTileId).Select(t => t.Id).ToHashSet();

        BuildButton.IsEnabled = mine;
        RecruitButton.IsEnabled = mine;
        AttackButton.IsEnabled = mine && _attackTargets.Count > 0;
    }

    private async void OnBuildClicked(object? sender, EventArgs e)
    {
        if (_selectedTileId is null) return;
        var buildings = _session.State.Content.Buildings.Values.ToList();
        var names = buildings.Select(b => $"{b.Name}（金{b.Cost.Gold}/{b.BuildTurns}月）").ToArray();
        string choice = await DisplayActionSheet("建造建筑", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        var r = _session.World.Build(_session.PlayerFactionId, _selectedTileId, buildings[idx].Id);
        await DisplayAlert("建造", r.Message, "确定");
        RefreshHud();
        UpdateSelectionUi();
        MapCanvas.InvalidateSurface();
    }

    private async void OnRecruitClicked(object? sender, EventArgs e)
    {
        if (_selectedTileId is null) return;
        var units = _session.PlayerFaction.Def.RecruitableUnitIds
            .Select(id => _session.State.Content.Units[id]).ToList();
        var names = units.Select(u => $"{u.Name}（金{u.RecruitCost.Gold} 粮{u.RecruitCost.Food}）").ToArray();
        string choice = await DisplayActionSheet("招募兵种", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        string countStr = await DisplayPromptAsync("招募数量", $"招募多少 {units[idx].Name}？", initialValue: "1", keyboard: Keyboard.Numeric);
        if (!int.TryParse(countStr, out int count) || count <= 0) return;

        var r = _session.World.Recruit(_session.PlayerFactionId, _selectedTileId, units[idx].Id, count);
        await DisplayAlert("招募", r.Message, "确定");
        RefreshHud();
        UpdateSelectionUi();
        MapCanvas.InvalidateSurface();
    }

    private async void OnAttackClicked(object? sender, EventArgs e)
    {
        if (_selectedTileId is null || _attackTargets.Count == 0) return;
        var targets = _attackTargets.Select(id => _session.State.Tiles[id]).ToList();
        var names = targets.Select(t => $"{t.Name}（{OwnerName(t)} 兵{t.Units.Count}）").ToArray();
        string choice = await DisplayActionSheet("出征目标", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        var pending = _session.StartPlayerAttack(_selectedTileId, targets[idx].Id);
        await Navigation.PushAsync(new BattlePage(_session, pending));
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            await SaveStore.SaveAsync(_session);
            await DisplayAlert("保存", $"已保存（第 {_session.State.Month} 月）。", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("保存失败", ex.Message, "确定");
        }
    }

    private async void OnEndTurnClicked(object? sender, EventArgs e)
    {
        var report = _session.EndMonth();
        _selectedTileId = null;
        RefreshHud();
        UpdateSelectionUi();
        MapCanvas.InvalidateSurface();

        if (report.CompletedBuildings.Count > 0)
            await DisplayAlert("本月完工", string.Join("\n", report.CompletedBuildings), "确定");

        CheckGameOver();
    }

    private async void CheckGameOver()
    {
        var state = _session.State;
        bool playerAlive = state.IsAlive(_session.PlayerFactionId);
        bool enemiesAlive = state.Factions.Values
            .Any(f => f.Kind == FactionKind.Ai && state.IsAlive(f.Id));

        if (!playerAlive)
        {
            await DisplayAlert("战败", "你已失去所有地盘。", "返回主菜单");
            await Navigation.PopToRootAsync();
        }
        else if (!enemiesAlive)
        {
            await DisplayAlert("胜利", "敌对诸侯已被消灭，天下归一！", "返回主菜单");
            await Navigation.PopToRootAsync();
        }
    }

    private string OwnerName(TileState tile)
    {
        if (tile.IsRebelFixed) return "反贼";
        return _session.State.Factions.TryGetValue(tile.OwnerFactionId, out var f) ? f.Def.Name : "中立";
    }

    private static string TypeName(TileType type) => type switch
    {
        TileType.City => "城池",
        TileType.Pass => "关隘",
        TileType.Village => "村庄",
        _ => type.ToString(),
    };
}

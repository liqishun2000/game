using MauiApp.Game.App;
using MauiApp.Game.Model;
using MauiApp.Game.World.State;
using MauiApp.Rendering;
using MauiApp.Services;
using MauiApp.Tutorial;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MauiApp.Pages;

public partial class WorldMapPage : ContentPage
{
    private readonly GameSession _session;
    private readonly WorldMapRenderer _renderer = new();
    private readonly HudRenderer _hud = new();
    private readonly AudioService _audio;
    private readonly List<FloatingText> _feedback = new();
    private readonly LevelObjectives _objectives;

    private MapLayout _layout = new();
    private AnimationClock? _clock;
    private string? _selectedTileId;
    private HashSet<string> _attackTargets = new();

    private string[] _coachSteps = Array.Empty<string>();
    private int _coachIndex;

    public WorldMapPage(GameSession session)
    {
        InitializeComponent();
        _session = session;
        _audio = ServiceHelper.Get<AudioService>();
        _objectives = new LevelObjectives(session.State.MapId);
        BuildObjectivePanel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await PixelFont.EnsureLoadedAsync();
        await _audio.PreloadAsync(new[] { AudioKeys.BgmWorld, AudioKeys.SfxClick, AudioKeys.SfxConfirm, AudioKeys.SfxBuild, AudioKeys.SfxCoin });
        await _audio.PlayBgmAsync(AudioKeys.BgmWorld);

        _clock ??= new AnimationClock(Dispatcher, Redraw) { AlwaysAnimate = true };
        _clock.Wake();

        RefreshAll();
        CheckGameOver();
        MaybeStartCoach();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _clock?.Stop();
    }

    private void Redraw()
    {
        for (int i = _feedback.Count - 1; i >= 0; i--)
            if (!_feedback[i].Advance(1f / 60f)) _feedback.RemoveAt(i);
        MapCanvas.InvalidateSurface();
        HudCanvas.InvalidateSurface();
    }

    private void RefreshAll()
    {
        _objectives.Evaluate(_session);
        UpdateObjectivePanel();
        UpdateSelectionUi();
        MapCanvas.InvalidateSurface();
        HudCanvas.InvalidateSurface();
    }

    // ---------- 关卡目标 ----------
    private void BuildObjectivePanel()
    {
        foreach (var obj in _objectives.Items)
        {
            ObjectiveList.Add(new Label
            {
                Style = (Style)Application.Current!.Resources["PixelLabel"],
                FontSize = 13,
            });
        }
        UpdateObjectivePanel();
    }

    private void UpdateObjectivePanel()
    {
        // 第 0 个子项是标题
        for (int i = 0; i < _objectives.Items.Count; i++)
        {
            if (ObjectiveList.Children[i + 1] is not Label label) continue;
            var obj = _objectives.Items[i];
            label.Text = (obj.Done ? "✓ " : "▸ ") + obj.Text;
            label.TextColor = obj.Done
                ? (Color)Application.Current!.Resources["PixelJade"]
                : (Color)Application.Current!.Resources["PixelParchmentText"];
        }
    }

    // ---------- 新手引导 ----------
    private void MaybeStartCoach()
    {
        string key = "world." + _session.State.MapId;
        if (SettingsStore.IsTutorialDone(key)) return;

        _coachSteps = _session.State.MapId == "v1_countryside"
            ? new[]
            {
                "欢迎来到「乡野初阵」！你统领刘备、关羽，驻守蓝色地块「玄德屯」。",
                "先点选「玄德屯」，再点底部「招兵」补充兵力，壮大队伍。",
                "选中「玄德屯」后点「出征」，选择相邻的贼寨「北乡」发起进攻。",
                "战斗中：点蓝色高亮格移动，点红圈敌人发起攻击；击溃守军即可占领。",
                "目标：攻占北乡，再一路向东击败「敌寨」诸侯，平定乡野。旗开得胜！",
            }
            : new[] { "选择你的地块进行经营与出征，消灭所有敌对诸侯即可获胜。" };

        _coachIndex = 0;
        ShowCoachStep();
    }

    private void ShowCoachStep()
    {
        CoachText.Text = _coachSteps[_coachIndex];
        CoachNext.Text = _coachIndex >= _coachSteps.Length - 1 ? "开始" : "下一步";
        CoachOverlay.IsVisible = true;
    }

    private void OnCoachNext(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        if (_coachIndex >= _coachSteps.Length - 1)
        {
            FinishCoach();
            return;
        }
        _coachIndex++;
        ShowCoachStep();
    }

    private void OnCoachSkip(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxCancel);
        FinishCoach();
    }

    private void FinishCoach()
    {
        CoachOverlay.IsVisible = false;
        SettingsStore.SetTutorialDone("world." + _session.State.MapId);
    }

    private void OnPaintHud(object? sender, SKPaintSurfaceEventArgs e)
    {
        var f = _session.PlayerFaction;
        _hud.Draw(e.Surface.Canvas, e.Info, _session.State.Month, f.Gold, f.Food, f.TechPoints, f.Prison.Count);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _layout = MapLayout.Build(_session.State, e.Info.Width, e.Info.Height);
        _renderer.Draw(e.Surface.Canvas, e.Info, _session.State, _layout, _selectedTileId, _attackTargets,
            _clock?.TimeSeconds ?? 0f, _feedback);
    }

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType == SKTouchAction.Pressed)
        {
            var hit = _layout.HitTest(e.Location.X, e.Location.Y);
            if (hit is not null)
            {
                _audio.PlaySfx(AudioKeys.SfxClick);
                _selectedTileId = hit;
                UpdateSelectionUi();
                MapCanvas.InvalidateSurface();
            }
        }

        e.Handled = true;
    }

    private void Feedback(string text, SKColor color)
    {
        SKPoint at = _selectedTileId is not null && _layout.Positions.TryGetValue(_selectedTileId, out var p)
            ? new SKPoint(p.X, p.Y - _layout.NodeRadius)
            : new SKPoint((float)MapCanvas.Width / 2, (float)MapCanvas.Height / 2);

        _feedback.Add(new FloatingText
        {
            Text = text, Color = color, X = at.X, Y = at.Y,
            SizeFactor = 0.34f, Life = 1.2f, RiseSpeed = 34f,
        });
        _clock?.Wake();
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
        _audio.PlaySfx(AudioKeys.SfxClick);
        var buildings = _session.State.Content.Buildings.Values.ToList();
        var names = buildings.Select(b => $"{b.Name}（金{b.Cost.Gold}/{b.BuildTurns}月）").ToArray();
        string choice = await DisplayActionSheetAsync("建造建筑", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        var r = _session.World.Build(_session.PlayerFactionId, _selectedTileId, buildings[idx].Id);
        if (r.Success)
        {
            _audio.PlaySfx(AudioKeys.SfxBuild);
            Feedback($"开建 {buildings[idx].Name}", new SKColor(0x8a, 0xd0, 0xff));
        }
        else
        {
            _audio.PlaySfx(AudioKeys.SfxCancel);
            Feedback(r.Message, new SKColor(0xff, 0x6a, 0x4a));
        }
        RefreshAll();
    }

    private async void OnRecruitClicked(object? sender, EventArgs e)
    {
        if (_selectedTileId is null) return;
        _audio.PlaySfx(AudioKeys.SfxClick);
        var units = _session.PlayerFaction.Def.RecruitableUnitIds
            .Select(id => _session.State.Content.Units[id]).ToList();
        var names = units.Select(u => $"{u.Name}（金{u.RecruitCost.Gold} 粮{u.RecruitCost.Food}）").ToArray();
        string choice = await DisplayActionSheetAsync("招募兵种", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        string countStr = await DisplayPromptAsync("招募数量", $"招募多少 {units[idx].Name}？", initialValue: "1", keyboard: Keyboard.Numeric);
        if (!int.TryParse(countStr, out int count) || count <= 0) return;

        var r = _session.World.Recruit(_session.PlayerFactionId, _selectedTileId, units[idx].Id, count);
        if (r.Success)
        {
            _objectives.MarkRecruited();
            _audio.PlaySfx(AudioKeys.SfxCoin);
            Feedback($"招募 {units[idx].Name}×{count}", new SKColor(0x8a, 0xd0, 0x6a));
        }
        else
        {
            _audio.PlaySfx(AudioKeys.SfxCancel);
            Feedback(r.Message, new SKColor(0xff, 0x6a, 0x4a));
        }
        RefreshAll();
    }

    private async void OnTechClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        var faction = _session.PlayerFaction;
        var available = _session.State.Content.Techs.Values
            .Where(t => !faction.ResearchedTechIds.Contains(t.Id)
                        && t.PrereqIds.All(p => faction.ResearchedTechIds.Contains(p)))
            .ToList();

        if (available.Count == 0)
        {
            await DisplayAlertAsync("科技", "暂无可研究的科技。", "确定");
            return;
        }

        var names = available.Select(t => $"{t.Name}（科技{t.Cost.TechPoints} 金{t.Cost.Gold}）").ToArray();
        string choice = await DisplayActionSheetAsync("研究科技", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        var r = _session.World.Research(_session.PlayerFactionId, available[idx].Id);
        if (r.Success)
        {
            _audio.PlaySfx(AudioKeys.SfxConfirm);
            Feedback($"研究 {available[idx].Name}", new SKColor(0xe8, 0xb9, 0x48));
        }
        else
        {
            _audio.PlaySfx(AudioKeys.SfxCancel);
            Feedback(r.Message, new SKColor(0xff, 0x6a, 0x4a));
        }
        RefreshAll();
    }

    private async void OnAttackClicked(object? sender, EventArgs e)
    {
        if (_selectedTileId is null || _attackTargets.Count == 0) return;
        _audio.PlaySfx(AudioKeys.SfxConfirm);
        var targets = _attackTargets.Select(id => _session.State.Tiles[id]).ToList();
        var names = targets.Select(t => $"{t.Name}（{OwnerName(t)} 兵{t.Units.Count}）").ToArray();
        string choice = await DisplayActionSheetAsync("出征目标", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        var pending = _session.StartPlayerAttack(_selectedTileId, targets[idx].Id);
        await Navigation.PushAsync(new BattlePage(_session, pending));
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        try
        {
            await SaveStore.SaveAsync(_session);
            Feedback($"已保存（第 {_session.State.Month} 月）", new SKColor(0x8a, 0xd0, 0xff));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("保存失败", ex.Message, "确定");
        }
    }

    private async void OnEndTurnClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxConfirm);
        var report = _session.EndMonth();
        _selectedTileId = null;
        RefreshAll();
        Feedback($"第 {_session.State.Month} 月", new SKColor(0xe8, 0xb9, 0x48));

        if (report.CompletedBuildings.Count > 0)
            await DisplayAlertAsync("本月完工", string.Join("\n", report.CompletedBuildings), "确定");

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
            await DisplayAlertAsync("战败", "你已失去所有地盘。", "返回主菜单");
            await Navigation.PopToRootAsync();
        }
        else if (!enemiesAlive)
        {
            await DisplayAlertAsync("胜利", "敌对诸侯已被消灭，天下归一！", "返回主菜单");
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

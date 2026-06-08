using MauiApp.Game.App;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World;
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
    private readonly MapCamera _camera = new();

    private MapLayout _layout = new();
    private AnimationClock? _clock;
    private string? _selectedTileId;
    private HashSet<string> _attackTargets = new();
    private float _canvasW, _canvasH;
    private bool _cameraInit;

    private string[] _coachSteps = Array.Empty<string>();
    private int _coachIndex;
    private bool _enteringPendingBattle;

    public WorldMapPage(GameSession session)
    {
        InitializeComponent();
        _session = session;
        _audio = ServiceHelper.Get<AudioService>();
        _objectives = new LevelObjectives(session.State.MapId);
        BuildObjectivePanel();
        MapCanvasGestures.Attach(MapCanvas, OnMapPan, OnMapTap, OnMapPinch);
        ApplyObjectivePanelVisibility();
    }

    private void ApplyObjectivePanelVisibility()
    {
        bool show = SettingsStore.ShowObjectivePanel;
        ObjectivePanel.IsVisible = show;
        ObjectiveButton.IsVisible = !show;
    }

    private void OnObjectiveClose(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        SettingsStore.ShowObjectivePanel = false;
        ApplyObjectivePanelVisibility();
    }

    private void OnObjectiveShow(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        SettingsStore.ShowObjectivePanel = true;
        ApplyObjectivePanelVisibility();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        OrientationService.LockLandscape();
        ImmersiveService.Enable();
        await PixelFont.EnsureLoadedAsync();
        await GfxAssets.EnsureBattleCoreAsync();
        var portraitIds = _session.State.Tiles.Values.SelectMany(t => t.Generals).Select(g => g.TemplateId);
        await GfxAssets.PreloadPortraitsAsync(portraitIds);
        await _audio.PreloadAsync(new[] { AudioKeys.BgmWorld, AudioKeys.SfxClick, AudioKeys.SfxConfirm, AudioKeys.SfxBuild, AudioKeys.SfxCoin });
        await _audio.PlayBgmAsync(AudioKeys.BgmWorld);

        _clock ??= new AnimationClock(Dispatcher, Redraw) { AlwaysAnimate = true };
        _clock.Wake();

        RefreshAll();
        CheckGameOver();
        MaybeStartCoach();
        _ = EnterNextPendingBattleAsync();
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
        for (int i = 0; i < _objectives.Items.Count; i++)
        {
            if (ObjectiveList.Children[i] is not Label label) continue;
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
                "先点选「玄德屯」选中地盘，再点底部「招兵」补充兵力，壮大队伍。",
                "选中「玄德屯」后点「出征」，选择相邻的贼寨「北乡」并编队发起进攻。",
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
        _hud.Draw(e.Surface.Canvas, e.Info, _session.State.Month, f.Gold, f.Food, f.TechPoints, f.Prison.Count, SafeInsets.Top);
        PrisonButton.IsVisible = f.Prison.Count > 0;
    }

    private void OnMapPan(float dx, float dy)
    {
        _camera.Pan(dx, dy);
        _camera.Clamp(_canvasW, _canvasH, _layout.ContentWidth, _layout.ContentHeight);
        MapCanvas.InvalidateSurface();
    }

    private void OnMapPinch(float factor, float anchorX, float anchorY)
    {
        _camera.ZoomAt(anchorX, anchorY, factor, _canvasW, _canvasH, _layout.ContentWidth, _layout.ContentHeight);
        MapCanvas.InvalidateSurface();
    }

    private void OnMapTap(float x, float y)
    {
        var (wx, wy) = _camera.ScreenToWorld(x, y);
        var hit = _layout.HitTest(wx, wy);
        if (hit is null) return;

        _audio.PlaySfx(AudioKeys.SfxClick);
        _selectedTileId = hit;
        TileDetailPanel.IsVisible = false;
        UpdateSelectionUi();
        MapCanvas.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _canvasW = e.Info.Width;
        _canvasH = e.Info.Height;
        _layout = MapLayout.Build(_session.State, e.Info.Width, e.Info.Height);
        if (!_cameraInit)
        {
            var b = _layout.NodeBounds;
            _camera.FitToBounds(e.Info.Width, e.Info.Height,
                b.Left, b.Top, b.Width, b.Height,
                _layout.ContentWidth, _layout.ContentHeight);
            _cameraInit = true;
        }
        else
            _camera.Clamp(e.Info.Width, e.Info.Height, _layout.ContentWidth, _layout.ContentHeight);
        _renderer.Draw(e.Surface.Canvas, e.Info, _session.State, _layout, _selectedTileId, _attackTargets,
            _clock?.TimeSeconds ?? 0f, _camera, _feedback);
    }

    private void Feedback(string text, SKColor color)
    {
        SKPoint at;
        if (_selectedTileId is not null && _layout.Positions.TryGetValue(_selectedTileId, out var p))
        {
            float sx = p.X * _camera.Zoom - _camera.OffsetX;
            float sy = p.Y * _camera.Zoom - _camera.OffsetY;
            at = new SKPoint(sx, sy - _layout.NodeRadius);
        }
        else
            at = new SKPoint(_canvasW / 2, _canvasH / 2);

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
            DetailButton.IsEnabled = BuildButton.IsEnabled = RecruitButton.IsEnabled = AttackButton.IsEnabled = false;
            TileDetailPanel.IsVisible = false;
            _attackTargets = new();
            return;
        }

        bool mine = tile.OwnerFactionId == _session.PlayerFactionId && !tile.IsRebelFixed;
        var gens = string.Join("、", tile.Generals.Select(g =>
            g.ActedThisMonth ? $"{g.Template.Name}(已行动)" : g.Template.Name));
        TileLabel.Text =
            $"{tile.Name}（{TypeName(tile.Type)}）  归属:{OwnerName(tile)}\n" +
            $"建筑:{tile.Buildings.Count}  武将:{(gens.Length == 0 ? "无" : gens)}  小兵:{tile.Units.Count}";

        _attackTargets = _session.AttackTargets(_selectedTileId).Select(t => t.Id).ToHashSet();

        DetailButton.IsEnabled = true;
        BuildButton.IsEnabled = mine;
        RecruitButton.IsEnabled = mine;
        AttackButton.IsEnabled = mine && _attackTargets.Count > 0;

        if (TileDetailPanel.IsVisible)
            UpdateTileDetailPanel(tile);
    }

    private void OnDetailClicked(object? sender, EventArgs e)
    {
        if (_selectedTileId is null || !_session.State.Tiles.TryGetValue(_selectedTileId, out var tile))
            return;

        _audio.PlaySfx(AudioKeys.SfxClick);
        ShowTileDetailPanel(tile);
    }

    private void ShowTileDetailPanel(TileState tile)
    {
        UpdateTileDetailPanel(tile);
        TileDetailPanel.IsVisible = true;
    }

    private void UpdateTileDetailPanel(TileState tile)
    {
        DetailTitle.Text = $"{tile.Name}（{TypeName(tile.Type)}）";
        DetailOwner.Text = $"归属：{OwnerName(tile)}";

        DetailBuildings.Children.Clear();
        if (tile.Buildings.Count == 0)
            DetailBuildings.Children.Add(MakeDetailLabel("（无建筑）", dim: true));
        else
        {
            foreach (var b in tile.Buildings)
            {
                string status = b.IsComplete ? $"Lv{b.Level} 已完工" : $"建造中（剩 {b.RemainingTurns} 月）";
                DetailBuildings.Children.Add(MakeDetailLabel($"• {b.Template.Name}  {status}"));
            }
        }

        DetailGenerals.Children.Clear();
        if (tile.Generals.Count == 0)
            DetailGenerals.Children.Add(MakeDetailLabel("（无武将）", dim: true));
        else
        {
            foreach (var g in tile.Generals)
            {
                string acted = g.ActedThisMonth ? " [本月已行动]" : "";
                var portrait = new Image { WidthRequest = 28, HeightRequest = 28, Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Center };
                _ = GfxAssets.GetUiPortraitAsync(g.TemplateId).ContinueWith(t =>
                {
                    if (t.Result is not null)
                        MainThread.BeginInvokeOnMainThread(() => portrait.Source = t.Result);
                });
                var btn = new Button
                {
                    Text = $"{g.Template.Name}{acted}",
                    Style = (Style)Application.Current!.Resources["PixelButton"],
                    FontSize = 13,
                    Padding = new Thickness(10, 4),
                    HorizontalOptions = LayoutOptions.Start,
                };
                var captured = g;
                btn.Clicked += async (_, _) => await ShowGeneralDetailAsync(captured);
                DetailGenerals.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 6,
                    Children = { portrait, btn },
                });
            }
        }

        DetailUnits.Children.Clear();
        var groups = tile.Units
            .GroupBy(u => u.TemplateId)
            .Select(grp =>
            {
                var tpl = _session.State.Content.Units[grp.Key];
                return $"{tpl.Name} ×{grp.Count()}";
            })
            .ToList();
        if (groups.Count == 0)
            DetailUnits.Children.Add(MakeDetailLabel("（无驻军）", dim: true));
        else
            DetailUnits.Children.Add(MakeDetailLabel(string.Join("  ", groups)));
    }

    private static Label MakeDetailLabel(string text, bool dim = false) => new()
    {
        Text = text,
        Style = (Style)Application.Current!.Resources["PixelLabel"],
        FontSize = 13,
        TextColor = dim
            ? (Color)Application.Current!.Resources["PixelGoldDark"]
            : (Color)Application.Current!.Resources["PixelParchmentText"],
    };

    private async Task ShowGeneralDetailAsync(GeneralInstance g)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        string tileName = g.TileId is not null && _session.State.Tiles.TryGetValue(g.TileId, out var t) ? t.Name : "—";
        string equipLine = "无";
        if (g.EquipmentId is not null && _session.State.Content.Equipment.TryGetValue(g.EquipmentId, out var cur))
            equipLine = $"{cur.Name}\n{EquipService.DescribeEquipment(cur)}";

        var s = g.Template.MapStats;
        string body =
            $"驻地：{tileName}\n" +
            $"武力 {s.Wuli}  统率 {s.Tongshuai}  智力 {s.Zhili}\n" +
            $"政治 {s.Zhengzhi}  魅力 {s.Meili}  意志 {s.Yizhi}\n" +
            $"等级 Lv{g.Level}" +
            (g.ActedThisMonth ? "  ·本月已行动" : "") +
            $"\n\n装备：{equipLine}";

        string choice = await DisplayActionSheetAsync(g.Template.Name, "关闭", null,
            "更换装备", g.EquipmentId is not null ? "卸下装备" : null,
            g.EquipmentId is not null ? "装备详情" : null);

        if (choice == "更换装备")
            await PickAndEquipAsync(g);
        else if (choice == "卸下装备")
            await ApplyEquipResult(_session.Equip.Unequip(_session.PlayerFactionId, g.TemplateId));
        else if (choice == "装备详情" && g.EquipmentId is not null
                 && _session.State.Content.Equipment.TryGetValue(g.EquipmentId, out var eq))
            await DisplayAlertAsync(eq.Name, EquipService.DescribeEquipment(eq), "确定");
        else if (choice != "关闭")
            await DisplayAlertAsync(g.Template.Name, body, "确定");
    }

    private async Task PickAndEquipAsync(GeneralInstance general)
    {
        var options = _session.Equip.EquipOptionsFor(_session.PlayerFactionId, general).ToList();
        if (options.Count == 0)
        {
            await DisplayAlertAsync("更换装备", "武库暂无可用武将装备。\n战斗缴获或卸下后会入库。", "确定");
            return;
        }

        var labels = options.Select(o => $"{o.Eq.Name}（{o.SourceLabel}）").ToArray();
        string choice = await DisplayActionSheetAsync($"为 {general.Template.Name} 装备", "取消", null, labels);
        int idx = Array.IndexOf(labels, choice);
        if (idx < 0) return;

        await ApplyEquipResult(_session.Equip.Equip(_session.PlayerFactionId, general.TemplateId, options[idx].Eq.Id));
    }

    private async Task ApplyEquipResult(OperationResult r)
    {
        if (r.Success)
            _audio.PlaySfx(AudioKeys.SfxConfirm);
        else
            _audio.PlaySfx(AudioKeys.SfxCancel);

        Feedback(r.Message, r.Success ? new SKColor(0xe8, 0xb9, 0x48) : new SKColor(0xff, 0x6a, 0x4a));
        RefreshAll();
        if (TileDetailPanel.IsVisible && _selectedTileId is not null
            && _session.State.Tiles.TryGetValue(_selectedTileId, out var tile))
            UpdateTileDetailPanel(tile);
    }

    private async void OnArmoryClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        var armory = _session.Equip.ArmoryGeneralEquipment(_session.PlayerFactionId).ToList();
        var worn = _session.State.TilesOf(_session.PlayerFactionId)
            .SelectMany(t => t.Generals)
            .Where(g => g.EquipmentId is not null && g.Status == GeneralStatus.Active)
            .Select(g => (General: g, Eq: _session.State.Content.Equipment[g.EquipmentId!]))
            .ToList();

        if (armory.Count == 0 && worn.Count == 0)
        {
            await DisplayAlertAsync("武库", "暂无库存。卸下武将装备或战斗缴获后会入库。", "确定");
            return;
        }

        var entries = new List<string>();
        var eqIds = new List<string>();
        foreach (var eq in armory)
        {
            entries.Add($"【库】{eq.Name}");
            eqIds.Add(eq.Id);
        }
        foreach (var (g, eq) in worn)
        {
            entries.Add($"【{g.Template.Name}】{eq.Name}");
            eqIds.Add(eq.Id);
        }

        string pick = await DisplayActionSheetAsync("武库", "取消", null, entries.ToArray());
        int idx = entries.IndexOf(pick);
        if (idx < 0 || !_session.State.Content.Equipment.TryGetValue(eqIds[idx], out var detail))
            return;

        string action = await DisplayActionSheetAsync($"{detail.Name}\n{EquipService.DescribeEquipment(detail)}",
            "取消", null, "分配给武将");
        if (action != "分配给武将") return;

        var generals = _session.State.TilesOf(_session.PlayerFactionId)
            .SelectMany(t => t.Generals)
            .Where(g => g.Status == GeneralStatus.Active)
            .ToList();
        if (generals.Count == 0)
        {
            await DisplayAlertAsync("武库", "没有可装备的武将。", "确定");
            return;
        }

        var genNames = generals.Select(g =>
            $"{g.Template.Name}{(g.EquipmentId is not null ? "（已装备）" : "")}").ToArray();
        string genPick = await DisplayActionSheetAsync("选择武将", "取消", null, genNames);
        int gIdx = Array.IndexOf(genNames, genPick);
        if (gIdx < 0) return;

        await ApplyEquipResult(_session.Equip.Equip(_session.PlayerFactionId, generals[gIdx].TemplateId, eqIds[idx]));
    }

    private void OnDetailClose(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        TileDetailPanel.IsVisible = false;
    }

    private async void OnPrisonClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        var prisoners = _session.PlayerFaction.Prison;
        if (prisoners.Count == 0)
        {
            await DisplayAlertAsync("监狱", "当前无俘虏。", "确定");
            return;
        }

        var names = prisoners.Select(p =>
        {
            int meili = _session.State.TilesOf(_session.PlayerFactionId)
                .SelectMany(t => t.Generals).Where(g => g.Status == GeneralStatus.Active)
                .Select(g => g.Template.MapStats.Meili).DefaultIfEmpty(0).Max();
            bool loyal = p.Template.Traits.Contains("sizhong");
            double chance = StatCalculator.PersuadeChance(
                meili, p.Template.MapStats.Yizhi, p.DetainedMonths, loyal, 0, _session.State.Balance);
            return $"{p.Template.Name}（约 {(int)(chance * 100)}%）";
        }).ToArray();
        string choice = await DisplayActionSheetAsync("招降俘虏", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        var r = _session.Prison.Persuade(_session.PlayerFactionId, prisoners[idx].TemplateId);
        if (r.Success)
            _audio.PlaySfx(AudioKeys.SfxConfirm);
        else
            _audio.PlaySfx(AudioKeys.SfxCancel);

        Feedback(r.Message, r.Success ? new SKColor(0x8a, 0xd0, 0x6a) : new SKColor(0xff, 0x6a, 0x4a));
        RefreshAll();
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

        await Navigation.PushAsync(new ExpeditionPage(_session, _selectedTileId, targets[idx].Id));
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

        foreach (var log in report.AiActions.Take(3))
            Feedback(log, new SKColor(0xff, 0xa0, 0x60));

        if (report.CompletedBuildings.Count > 0)
            await DisplayAlertAsync("本月完工", string.Join("\n", report.CompletedBuildings), "确定");

        if (report.AiActions.Count > 0)
            await DisplayAlertAsync("本月战报", string.Join("\n", report.AiActions), "确定");

        CheckGameOver();
        await EnterNextPendingBattleAsync();
    }

    /// <summary>敌方进攻玩家时进入战斗页；多场战斗从战斗返回后由 OnAppearing 继续。</summary>
    private async Task EnterNextPendingBattleAsync()
    {
        if (_enteringPendingBattle || !_session.TryTakeNextPlayerBattle(out var pending) || pending is null)
            return;

        _enteringPendingBattle = true;
        try
        {
            var from = _session.State.Tiles[pending.AttackerTileId];
            var to = _session.State.Tiles[pending.DefenderTileId];
            string attackerName = _session.State.Factions.TryGetValue(pending.AttackerFactionId, out var af)
                ? af.Def.Name : "敌军";
            await DisplayAlertAsync("敌军来犯", $"{attackerName} 自「{from.Name}」进攻我方「{to.Name}」！", "迎战");
            _audio.PlaySfx(AudioKeys.SfxConfirm);
            await Navigation.PushAsync(new BattlePage(_session, pending));
        }
        finally
        {
            _enteringPendingBattle = false;
        }
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

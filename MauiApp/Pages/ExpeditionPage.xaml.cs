using MauiApp.Game.App;
using MauiApp.Game.Model;
using MauiApp.Game.World;
using MauiApp.Game.World.State;
using MauiApp.Rendering;
using MauiApp.Services;

namespace MauiApp.Pages;

public partial class ExpeditionPage : ContentPage
{
    private readonly GameSession _session;
    private readonly string _fromTileId;
    private readonly string _toTileId;
    private readonly AudioService _audio;

    private readonly Dictionary<string, CheckBox> _generalChecks = new();
    private readonly Dictionary<string, CheckBox> _unitChecks = new();

    public ExpeditionPage(GameSession session, string fromTileId, string toTileId)
    {
        InitializeComponent();
        _session = session;
        _fromTileId = fromTileId;
        _toTileId = toTileId;
        _audio = ServiceHelper.Get<AudioService>();
        BuildForm();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrientationService.LockLandscape();
        ImmersiveService.Enable();
        UpdatePowerPreview();
    }

    private void BuildForm()
    {
        var from = _session.State.Tiles[_fromTileId];
        var to = _session.State.Tiles[_toTileId];
        string owner = to.IsRebelFixed ? "反贼"
            : _session.State.Factions.TryGetValue(to.OwnerFactionId, out var f) ? f.Def.Name : "中立";

        TitleLabel.Text = $"{from.Name} → {to.Name}";
        SubtitleLabel.Text = $"目标：{owner}  武将{to.Generals.Count}  小兵{to.Units.Count}";

        foreach (var g in from.Generals.Where(x => !x.ActedThisMonth))
        {
            var cb = new CheckBox { IsChecked = true };
            cb.CheckedChanged += (_, _) => UpdatePowerPreview();
            _generalChecks[g.TemplateId] = cb;
            var portrait = new Image { WidthRequest = 32, HeightRequest = 32, Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Center };
            _ = BindPortraitAsync(g.TemplateId, portrait);
            GeneralList.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    portrait,
                    cb,
                    new Label
                    {
                        Text = g.Template.Name,
                        Style = (Style)Application.Current!.Resources["PixelLabel"],
                        FontSize = 14,
                        VerticalOptions = LayoutOptions.Center,
                    },
                },
            });
        }

        if (_generalChecks.Count == 0)
            GeneralList.Add(MakeHint("（本月可用武将均已行动）"));

        foreach (var grp in from.Units.GroupBy(u => u.TemplateId))
        {
            var tpl = _session.State.Content.Units[grp.Key];
            var cb = new CheckBox { IsChecked = true };
            cb.CheckedChanged += (_, _) => UpdatePowerPreview();
            _unitChecks[grp.Key] = cb;
            UnitList.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    cb,
                    new Label
                    {
                        Text = $"{tpl.Name} ×{grp.Count()}",
                        Style = (Style)Application.Current!.Resources["PixelLabel"],
                        FontSize = 14,
                        VerticalOptions = LayoutOptions.Center,
                    },
                },
            });
        }

        if (_unitChecks.Count == 0)
            UnitList.Add(MakeHint("（无驻军可带）"));

        int stock = _session.PlayerFaction.Food;
        int unitCount = from.Generals.Count(g => !g.ActedThisMonth) + from.Units.Count;
        int suggested = ExpeditionPlanner.SuggestedFood(unitCount, _session.State.Balance);
        FoodSlider.Maximum = stock;
        FoodSlider.Value = Math.Min(stock, suggested);
        FoodHintLabel.Text = $"粮库 {stock}  ·  建议 {suggested}（约 10 回合消耗）";
        UpdateFoodLabel();
    }

    private void UpdateFoodLabel()
    {
        FoodValueLabel.Text = $"携带 {(int)FoodSlider.Value} 粮";
    }

    private static async Task BindPortraitAsync(string templateId, Image image)
    {
        var src = await GfxAssets.GetUiPortraitAsync(templateId);
        if (src is not null) image.Source = src;
    }

    private static Label MakeHint(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current!.Resources["PixelLabel"],
        FontSize = 13,
        TextColor = (Color)Application.Current.Resources["PixelGoldDark"],
    };

    private void OnFoodChanged(object? sender, ValueChangedEventArgs e) => UpdateFoodLabel();

    private void UpdatePowerPreview()
    {
        var from = _session.State.Tiles[_fromTileId];
        var to = _session.State.Tiles[_toTileId];
        var setup = BuildSetup(from);

        var atkGens = from.Generals.Where(g => setup.GeneralTemplateIds.Contains(g.TemplateId)).ToList();
        var atkUnits = from.Units.Where(u => setup.UnitWorldIds.Contains(u.Id)).ToList();
        int atkPower = ExpeditionPlanner.EstimatePower(atkGens, atkUnits, _session.State.Content, _session.State.Balance);
        int defPower = ExpeditionPlanner.EstimatePower(to.Generals, to.Units, _session.State.Content, _session.State.Balance);

        double ratio = defPower <= 0 ? 1.0 : (double)atkPower / (atkPower + defPower);
        PowerBar.Progress = ratio;
        PowerLabel.Text = $"我方 {atkPower}  vs  敌方 {defPower}";
    }

    private ExpeditionSetup BuildSetup(TileState from)
    {
        var genIds = _generalChecks.Where(kv => kv.Value.IsChecked).Select(kv => kv.Key).ToList();
        var unitIds = new List<int>();
        foreach (var grp in from.Units.GroupBy(u => u.TemplateId))
        {
            if (_unitChecks.TryGetValue(grp.Key, out var cb) && cb.IsChecked)
                unitIds.AddRange(grp.Select(u => u.Id));
        }

        return new ExpeditionSetup
        {
            GeneralTemplateIds = genIds,
            UnitWorldIds = unitIds,
            CarriedFood = (int)FoodSlider.Value,
        };
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxConfirm);
        var setup = BuildSetup(_session.State.Tiles[_fromTileId]);
        var validation = ExpeditionPlanner.Validate(_session.State, _fromTileId, setup);
        if (!validation.Success)
        {
            _audio.PlaySfx(AudioKeys.SfxCancel);
            await DisplayAlertAsync("无法出征", validation.Message, "确定");
            return;
        }

        try
        {
            var pending = _session.StartPlayerAttack(_fromTileId, _toTileId, setup);
            await Navigation.PushAsync(new BattlePage(_session, pending));
            Navigation.RemovePage(this);
        }
        catch (Exception ex)
        {
            _audio.PlaySfx(AudioKeys.SfxCancel);
            await DisplayAlertAsync("出征失败", ex.Message, "确定");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        await Navigation.PopAsync();
    }
}

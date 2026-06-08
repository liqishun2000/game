using MauiApp.Game.App;
using MauiApp.Game.Content;
using MauiApp.Game.Model;
using MauiApp.Rendering;
using MauiApp.Services;
using SkiaSharp.Views.Maui;

namespace MauiApp.Pages;

public partial class MainMenuPage : ContentPage
{
    private const string DefaultMapId = "v1_countryside";

    private ContentLoadResult? _content;
    private string _mapId = DefaultMapId;

    private AiDifficulty _difficulty = AiDifficulty.Normal;

    private readonly MenuBackgroundRenderer _bg = new();
    private readonly AudioService _audio;
    private AnimationClock? _clock;

    public MainMenuPage()
    {
        InitializeComponent();
        _audio = ServiceHelper.Get<AudioService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ImmersiveService.Enable();
        OrientationService.LockLandscape();
        MenuLayout.Padding = new Thickness(24, 16 + SafeInsets.Top, 24, 16 + SafeInsets.Bottom);

        MuteSwitch.IsToggled = SettingsStore.Muted;
        VolumeSlider.Value = SettingsStore.BgmVolume;

        _clock ??= new AnimationClock(Dispatcher, () => BgCanvas.InvalidateSurface()) { AlwaysAnimate = true };
        _clock.Wake();

        await _audio.PreloadAsync(new[] { AudioKeys.BgmMenu, AudioKeys.SfxClick, AudioKeys.SfxConfirm });
        await _audio.PlayBgmAsync(AudioKeys.BgmMenu);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _clock?.Stop();
    }

    private void OnPaintBackground(object? sender, SKPaintSurfaceEventArgs e) =>
        _bg.Draw(e.Surface.Canvas, e.Info, _clock?.TimeSeconds ?? 0f);

    private void OnMuteToggled(object? sender, ToggledEventArgs e) => _audio.SetMuted(e.Value);

    private void OnVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        SettingsStore.BgmVolume = e.NewValue;
        SettingsStore.SfxVolume = Math.Clamp(e.NewValue + 0.2, 0, 1);
        _audio.ApplyBgmVolume();
    }

    private async Task<ContentLoadResult?> EnsureContentAsync()
    {
        if (_content is not null) return _content;
        var loaded = await ContentProvider.LoadAsync();
        if (!loaded.Success)
        {
            StatusLabel.Text = "内容校验失败：\n" + loaded.Validation;
            return null;
        }

        _content = loaded;
        return loaded;
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxConfirm);
        StartButton.IsEnabled = false;
        StatusLabel.Text = "正在加载游戏内容…";

        try
        {
            var content = await EnsureContentAsync();
            if (content is null) return;

            if (!content.Database.Maps.ContainsKey(_mapId))
                _mapId = content.Database.Maps.Keys.First();

            int seed = Environment.TickCount;
            var session = GameSession.Start(content.Database, _mapId, seed, _difficulty);
            StatusLabel.Text = "";
            await Navigation.PushAsync(new WorldMapPage(session));
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "启动失败：" + ex.Message;
        }
        finally
        {
            StartButton.IsEnabled = true;
        }
    }

    private async void OnLoadClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        if (!SaveStore.Exists)
        {
            StatusLabel.Text = "没有找到存档。";
            return;
        }

        LoadButton.IsEnabled = false;
        StatusLabel.Text = "正在读取存档…";
        try
        {
            var session = await SaveStore.LoadAsync();
            StatusLabel.Text = "";
            await Navigation.PushAsync(new WorldMapPage(session));
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "读取失败：" + ex.Message;
        }
        finally
        {
            LoadButton.IsEnabled = true;
        }
    }

    private async void OnSelectMapClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        var content = await EnsureContentAsync();
        if (content is null) return;

        var maps = content.Database.Maps.Values.ToList();
        var names = maps.Select(m => string.IsNullOrEmpty(m.Name) ? m.Id : $"{m.Name}（{m.Id}）").ToArray();
        string choice = await DisplayActionSheetAsync("选择地图", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;

        _mapId = maps[idx].Id;
        StatusLabel.Text = $"已选择地图：{names[idx]}\n（自定义地图可放入 {ContentProvider.UserMapsDirectory}）";
    }

    private async void OnDifficultyClicked(object? sender, EventArgs e)
    {
        _audio.PlaySfx(AudioKeys.SfxClick);
        var names = new[] { "简单", "普通", "困难" };
        string choice = await DisplayActionSheetAsync("AI 难度", "取消", null, names);
        int idx = Array.IndexOf(names, choice);
        if (idx < 0) return;
        _difficulty = idx switch
        {
            0 => AiDifficulty.Easy,
            2 => AiDifficulty.Hard,
            _ => AiDifficulty.Normal,
        };
        StatusLabel.Text = $"难度：{names[idx]}";
        DifficultyButton.Text = $"AI 难度：{names[idx]}";
    }
}

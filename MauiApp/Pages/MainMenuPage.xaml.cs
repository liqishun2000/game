using MauiApp.Game.App;
using MauiApp.Game.Content;
using MauiApp.Game.Model;
using MauiApp.Services;

namespace MauiApp.Pages;

public partial class MainMenuPage : ContentPage
{
    private const string DefaultMapId = "v1_countryside";

    private ContentLoadResult? _content;
    private string _mapId = DefaultMapId;

    public MainMenuPage()
    {
        InitializeComponent();
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
        StartButton.IsEnabled = false;
        StatusLabel.Text = "正在加载游戏内容…";

        try
        {
            var content = await EnsureContentAsync();
            if (content is null) return;

            if (!content.Database.Maps.ContainsKey(_mapId))
                _mapId = content.Database.Maps.Keys.First();

            int seed = Environment.TickCount;
            var session = GameSession.Start(content.Database, _mapId, seed, AiDifficulty.Normal);
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
}

using MauiApp.Game.App;
using MauiApp.Game.Model;
using MauiApp.Services;

namespace MauiApp.Pages;

public partial class MainMenuPage : ContentPage
{
    private const string MapId = "v1_countryside";

    public MainMenuPage()
    {
        InitializeComponent();
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        StartButton.IsEnabled = false;
        StatusLabel.Text = "正在加载游戏内容…";

        try
        {
            var content = await ContentProvider.LoadAsync();
            if (!content.Success)
            {
                StatusLabel.Text = "内容校验失败：\n" + content.Validation;
                return;
            }

            int seed = Environment.TickCount;
            var session = GameSession.Start(content.Database, MapId, seed, AiDifficulty.Normal);
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

    private void OnSelectMapClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = "v1 暂仅内置「乡野」地图，自定义地图见后续里程碑。";
    }
}

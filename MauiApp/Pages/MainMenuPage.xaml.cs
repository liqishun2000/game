namespace MauiApp.Pages;

public partial class MainMenuPage : ContentPage
{
    public MainMenuPage()
    {
        InitializeComponent();
    }

    private void OnStartClicked(object? sender, EventArgs e)
    {
        // M3 接入大地图页面后跳转到 WorldMapPage。
        StatusLabel.Text = "大地图尚未实现（里程碑 M3）。";
    }

    private void OnSelectMapClicked(object? sender, EventArgs e)
    {
        // M1/M9 接入地图选择（含玩家扩展地图）。
        StatusLabel.Text = "地图选择尚未实现（里程碑 M1/M9）。";
    }
}

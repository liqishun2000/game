namespace MauiApp.Game.Model;

/// <summary>
/// 大地图六维：武力、统帅、智力、政治、魅力、意志（约 1~100）。
/// </summary>
public sealed class MapStats
{
    public int Wuli { get; set; }
    public int Tongshuai { get; set; }
    public int Zhili { get; set; }
    public int Zhengzhi { get; set; }
    public int Meili { get; set; }
    public int Yizhi { get; set; }
}

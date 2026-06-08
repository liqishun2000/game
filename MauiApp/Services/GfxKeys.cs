namespace MauiApp.Services;

/// <summary>像素贴图资源 key（= Resources/Raw/ 下逻辑路径）。</summary>
public static class GfxKeys
{
    public const string GrassA = "art/tiles/grass_a.png";
    public const string GrassB = "art/tiles/grass_b.png";
    public const string Forest = "art/tiles/forest.png";
    public const string Water = "art/tiles/water.png";
    public const string Mountain = "art/tiles/mountain.png";
    public const string Road = "art/tiles/road.png";
    public const string Fort = "art/tiles/fort.png";

    public const string SoldierAtk = "art/units/soldier_atk.png";
    public const string SoldierDef = "art/units/soldier_def.png";
    public const string GeneralAtk = "art/units/general_atk.png";
    public const string GeneralDef = "art/units/general_def.png";

    public static string Portrait(string generalTemplateId) => $"art/portraits/{generalTemplateId}.png";

    public static readonly string[] BattleTiles =
    {
        GrassA, GrassB, Forest, Water, Mountain, Road, Fort,
        SoldierAtk, SoldierDef, GeneralAtk, GeneralDef,
    };
}

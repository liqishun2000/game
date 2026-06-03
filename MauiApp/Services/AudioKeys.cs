namespace MauiApp.Services;

/// <summary>
/// 音频资源 key（= <c>Resources/Raw/</c> 下逻辑路径）。文件缺失时 <see cref="AudioService"/> 静默降级。
/// 放置约定：BGM 用 .mp3/.ogg，SFX 用 .wav，落到 <c>Resources/Raw/audio/</c>。
/// </summary>
public static class AudioKeys
{
    public const string BgmMenu = "audio/bgm_menu.wav";
    public const string BgmWorld = "audio/bgm_world.wav";
    public const string BgmBattle = "audio/bgm_battle.wav";

    public const string SfxClick = "audio/sfx_click.wav";
    public const string SfxConfirm = "audio/sfx_confirm.wav";
    public const string SfxCancel = "audio/sfx_cancel.wav";
    public const string SfxMove = "audio/sfx_move.wav";
    public const string SfxHit = "audio/sfx_hit.wav";
    public const string SfxArrow = "audio/sfx_arrow.wav";
    public const string SfxDown = "audio/sfx_down.wav";
    public const string SfxVictory = "audio/sfx_victory.wav";
    public const string SfxDefeat = "audio/sfx_defeat.wav";
    public const string SfxBuild = "audio/sfx_build.wav";
    public const string SfxCoin = "audio/sfx_coin.wav";

    public static readonly string[] All =
    {
        BgmMenu, BgmWorld, BgmBattle,
        SfxClick, SfxConfirm, SfxCancel, SfxMove, SfxHit, SfxArrow, SfxDown,
        SfxVictory, SfxDefeat, SfxBuild, SfxCoin,
    };
}

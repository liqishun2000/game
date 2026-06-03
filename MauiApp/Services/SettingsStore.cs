namespace MauiApp.Services;

/// <summary>
/// 玩家设置（音量 / 静音等），用 MAUI <see cref="Preferences"/> 持久化。
/// 供 <see cref="AudioService"/> 与设置界面读写。
/// </summary>
public static class SettingsStore
{
    private const string KeyMuted = "audio.muted";
    private const string KeyBgmVolume = "audio.bgm";
    private const string KeySfxVolume = "audio.sfx";

    public static bool Muted
    {
        get => Preferences.Get(KeyMuted, false);
        set => Preferences.Set(KeyMuted, value);
    }

    /// <summary>背景音乐音量 0..1。</summary>
    public static double BgmVolume
    {
        get => Preferences.Get(KeyBgmVolume, 0.6);
        set => Preferences.Set(KeyBgmVolume, Math.Clamp(value, 0, 1));
    }

    /// <summary>音效音量 0..1。</summary>
    public static double SfxVolume
    {
        get => Preferences.Get(KeySfxVolume, 0.8);
        set => Preferences.Set(KeySfxVolume, Math.Clamp(value, 0, 1));
    }

    /// <summary>某段引导是否已完成（key 如 "world.v1_countryside"、"battle"）。</summary>
    public static bool IsTutorialDone(string key) => Preferences.Get("tutorial.done." + key, false);
    public static void SetTutorialDone(string key) => Preferences.Set("tutorial.done." + key, true);
}

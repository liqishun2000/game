using Plugin.Maui.Audio;

namespace MauiApp.Services;

/// <summary>
/// 音频服务：BGM 循环播放 + 一次性 SFX。基于 Plugin.Maui.Audio。
/// 音量/静音读自 <see cref="SettingsStore"/>。所有资源缺失/失败均静默降级，不影响游戏流程（见规划 4.3）。
/// 资源约定：BGM/SFX 文件放 <c>Resources/Raw/audio/</c>，key 用相对逻辑路径，如 "audio/bgm_menu.mp3"。
/// </summary>
public sealed class AudioService
{
    private readonly IAudioManager _manager;
    private readonly Dictionary<string, byte[]> _clips = new();
    private readonly List<IAudioPlayer> _activeSfx = new();

    private IAudioPlayer? _bgm;
    private string? _bgmKey;

    public AudioService(IAudioManager manager) => _manager = manager;

    /// <summary>预加载一组音频到内存（缺失忽略）。</summary>
    public async Task PreloadAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (_clips.ContainsKey(key)) continue;
            var bytes = await TryReadAsync(key);
            if (bytes is not null) _clips[key] = bytes;
        }
    }

    /// <summary>播放循环 BGM；同一曲目重复调用不重启。</summary>
    public async Task PlayBgmAsync(string key, bool loop = true)
    {
        if (_bgmKey == key && _bgm is { IsPlaying: true }) return;
        StopBgm();

        var bytes = await EnsureAsync(key);
        if (bytes is null) return;

        try
        {
            _bgm = _manager.CreatePlayer(new MemoryStream(bytes));
            _bgm.Loop = loop;
            _bgm.Volume = SettingsStore.Muted ? 0 : SettingsStore.BgmVolume;
            _bgm.Play();
            _bgmKey = key;
        }
        catch
        {
            _bgm = null;
            _bgmKey = null;
        }
    }

    public void StopBgm()
    {
        try { _bgm?.Stop(); _bgm?.Dispose(); }
        catch { /* ignore */ }
        _bgm = null;
        _bgmKey = null;
    }

    /// <summary>播放一次性音效（中途结束自动释放）。</summary>
    public void PlaySfx(string key)
    {
        if (SettingsStore.Muted) return;
        if (!_clips.TryGetValue(key, out var bytes)) return;

        try
        {
            var player = _manager.CreatePlayer(new MemoryStream(bytes));
            player.Volume = SettingsStore.SfxVolume;
            player.PlaybackEnded += (_, _) => Release(player);
            _activeSfx.Add(player);
            player.Play();
        }
        catch { /* ignore */ }
    }

    /// <summary>设置静音并即时作用于当前 BGM。</summary>
    public void SetMuted(bool muted)
    {
        SettingsStore.Muted = muted;
        if (_bgm is not null) _bgm.Volume = muted ? 0 : SettingsStore.BgmVolume;
    }

    public void ApplyBgmVolume()
    {
        if (_bgm is not null) _bgm.Volume = SettingsStore.Muted ? 0 : SettingsStore.BgmVolume;
    }

    private void Release(IAudioPlayer player)
    {
        _activeSfx.Remove(player);
        try { player.Dispose(); } catch { /* ignore */ }
    }

    private async Task<byte[]?> EnsureAsync(string key)
    {
        if (_clips.TryGetValue(key, out var cached)) return cached;
        var bytes = await TryReadAsync(key);
        if (bytes is not null) _clips[key] = bytes;
        return bytes;
    }

    private static async Task<byte[]?> TryReadAsync(string logicalPath)
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(logicalPath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}

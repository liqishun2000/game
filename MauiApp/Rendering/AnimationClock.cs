using System.Diagnostics;

namespace MauiApp.Rendering;

/// <summary>
/// 动画时钟：以 ~60fps 驱动重绘，但**仅在有活跃动画时**运行定时器，静止时停表以省电（见规划 4.2 / 10）。
/// 用法：<c>clock = new AnimationClock(dispatcher, () => canvas.InvalidateSurface());</c>，
/// 然后 <c>clock.Add(dt => tween.Advance(dt))</c> 添加每帧推进的轨道。
/// </summary>
public sealed class AnimationClock
{
    private readonly IDispatcher _dispatcher;
    private readonly Action _invalidate;
    private readonly List<Func<float, bool>> _tracks = new();
    private readonly Stopwatch _watch = new();
    private IDispatcherTimer? _timer;
    private long _lastTicks;

    /// <summary>若为 true，即使没有动画轨道也持续重绘（用于常驻呼吸高亮）。默认 false 省电。</summary>
    public bool AlwaysAnimate { get; set; }

    /// <summary>自时钟创建以来累计的秒数（用于环境脉冲等）。</summary>
    public float TimeSeconds => (float)_watch.Elapsed.TotalSeconds;

    public bool IsRunning => _timer?.IsRunning ?? false;

    public AnimationClock(IDispatcher dispatcher, Action invalidate)
    {
        _dispatcher = dispatcher;
        _invalidate = invalidate;
        _watch.Start();
    }

    /// <summary>添加一条每帧推进的轨道；返回 false 时自动移除。会唤醒时钟。</summary>
    public void Add(Func<float, bool> track)
    {
        _tracks.Add(track);
        Wake();
    }

    /// <summary>便捷：添加一个 Tween，每帧推进；完成后触发 onDone（可选）。</summary>
    public void Play(Tween tween, Action<float>? onUpdate = null, Action? onDone = null)
    {
        Add(dt =>
        {
            bool running = tween.Advance(dt);
            onUpdate?.Invoke(tween.Value);
            if (!running) onDone?.Invoke();
            return running;
        });
    }

    /// <summary>确保定时器在运行（有动画或常驻模式）。</summary>
    public void Wake()
    {
        if (IsRunning) return;
        _timer ??= CreateTimer();
        _lastTicks = _watch.ElapsedTicks;
        _timer.Start();
    }

    /// <summary>停止时钟并清空轨道（页面消失时调用）。</summary>
    public void Stop()
    {
        _timer?.Stop();
        _tracks.Clear();
    }

    private IDispatcherTimer CreateTimer()
    {
        var t = _dispatcher.CreateTimer();
        t.Interval = TimeSpan.FromMilliseconds(16);
        t.IsRepeating = true;
        t.Tick += OnTick;
        return t;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        long now = _watch.ElapsedTicks;
        float dt = (float)((now - _lastTicks) / (double)Stopwatch.Frequency);
        _lastTicks = now;
        if (dt <= 0) dt = 0.016f;

        for (int i = _tracks.Count - 1; i >= 0; i--)
        {
            bool keep;
            try { keep = _tracks[i](dt); }
            catch { keep = false; }
            if (!keep) _tracks.RemoveAt(i);
        }

        _invalidate();

        if (_tracks.Count == 0 && !AlwaysAnimate)
            _timer?.Stop();
    }
}

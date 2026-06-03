using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>
/// 战斗演出编排：把"瞬间状态变化"补放成动画（移动滑动、攻击冲刺、受击闪白/震屏、伤害飘字、阵亡淡出）。
/// 所有效果写入 <see cref="Vfx"/>，由 <see cref="BattleRenderer"/> 消费。引擎逻辑零侵入（见规划 7/10）。
/// </summary>
public sealed class BattleAnimator
{
    private readonly AnimationClock _clock;
    private readonly Random _rng = new();
    private bool _decayRunning;
    private float _shakeTime;
    private float _shakeTotal;
    private float _shakeMag;

    public BattleVfx Vfx { get; } = new();

    /// <summary>动画整体速度倍率（自动模式可调大以加速/跳过）。</summary>
    public float SpeedScale { get; set; } = 1f;

    public BattleAnimator(AnimationClock clock) => _clock = clock;

    /// <summary>滑动：从 from 格滑到 to 格（单位逻辑位置已是 to，用反向偏移补放）。</summary>
    public Task MoveAsync(int id, (int Col, int Row) from, (int Col, int Row) to)
    {
        float dx = from.Col - to.Col;
        float dy = from.Row - to.Row;
        int steps = Math.Abs(from.Col - to.Col) + Math.Abs(from.Row - to.Row);
        float dur = Math.Clamp(0.10f + 0.06f * steps, 0.12f, 0.5f) / SpeedScale;

        return RunAsync(dur, t =>
        {
            float e = 1 - Easing.OutQuad(t);
            Vfx.Offset[id] = new SKPoint(dx * e, dy * e);
        }, () => Vfx.Offset.Remove(id));
    }

    /// <summary>攻击冲刺：朝目标方向冲出再回位。</summary>
    public Task LungeAsync(int id, int dirCol, int dirRow)
    {
        float len = MathF.Max(1, MathF.Abs(dirCol) + MathF.Abs(dirRow));
        float nx = dirCol / len, ny = dirRow / len;
        float dur = 0.22f / SpeedScale;
        const float reach = 0.4f;

        return RunAsync(dur, t =>
        {
            float p = t < 0.5f ? t / 0.5f : 1 - (t - 0.5f) / 0.5f; // 0->1->0
            float e = Easing.OutQuad(p) * reach;
            Vfx.Offset[id] = new SKPoint(nx * e, ny * e);
        }, () => Vfx.Offset.Remove(id));
    }

    public void SpawnDamage(int col, int row, int dmg, bool crit, bool friendlyTarget)
    {
        EnsureDecay();
        var color = crit ? new SKColor(0xff, 0xd1, 0x40)
            : friendlyTarget ? new SKColor(0xff, 0x8a, 0x7a) : new SKColor(0xff, 0xff, 0xff);
        Vfx.Texts.Add(new FloatingText
        {
            Text = (crit ? "" : "") + dmg.ToString(),
            Color = color,
            X = col + 0.5f,
            Y = row + 0.15f,
            SizeFactor = crit ? 0.42f : 0.32f,
            Life = 0.85f / SpeedScale,
        });
    }

    public void SpawnLabel(int col, int row, string text, SKColor color)
    {
        EnsureDecay();
        Vfx.Texts.Add(new FloatingText
        {
            Text = text, Color = color, X = col + 0.5f, Y = row - 0.1f,
            SizeFactor = 0.36f, Life = 1.1f / SpeedScale, RiseSpeed = 0.7f,
        });
    }

    public void Flash(int id)
    {
        EnsureDecay();
        Vfx.Flash[id] = 1f;
    }

    public void Shake(float magnitudePx = 5f, float duration = 0.22f)
    {
        EnsureDecay();
        _shakeMag = magnitudePx;
        _shakeTotal = duration / SpeedScale;
        _shakeTime = _shakeTotal;
    }

    /// <summary>阵亡淡出：返回时这些单位已从 Vfx.Dying 移除。</summary>
    public Task DieAsync(IReadOnlyList<DyingUnit> units)
    {
        if (units.Count == 0) return Task.CompletedTask;
        foreach (var u in units) Vfx.Dying.Add(u);
        float dur = 0.34f / SpeedScale;

        return RunAsync(dur, t =>
        {
            float a = 1 - t;
            foreach (var u in units) u.Alpha = a;
        }, () =>
        {
            foreach (var u in units) Vfx.Dying.Remove(u);
        });
    }

    private Task RunAsync(float duration, Action<float> step, Action? onDone = null)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        float elapsed = 0;
        _clock.Add(dt =>
        {
            elapsed += dt;
            float t = Math.Clamp(elapsed / duration, 0f, 1f);
            step(t);
            if (t >= 1f)
            {
                onDone?.Invoke();
                tcs.TrySetResult();
                return false;
            }
            return true;
        });
        return tcs.Task;
    }

    private void EnsureDecay()
    {
        if (_decayRunning) return;
        _decayRunning = true;
        _clock.Add(dt =>
        {
            for (int i = Vfx.Texts.Count - 1; i >= 0; i--)
                if (!Vfx.Texts[i].Advance(dt)) Vfx.Texts.RemoveAt(i);

            if (Vfx.Flash.Count > 0)
                foreach (var key in Vfx.Flash.Keys.ToList())
                {
                    float v = Vfx.Flash[key] - dt * 4f;
                    if (v <= 0.01f) Vfx.Flash.Remove(key);
                    else Vfx.Flash[key] = v;
                }

            if (_shakeTime > 0)
            {
                _shakeTime -= dt;
                float mag = _shakeMag * Math.Max(0, _shakeTime / _shakeTotal);
                Vfx.Shake = new SKPoint(
                    (float)(_rng.NextDouble() * 2 - 1) * mag,
                    (float)(_rng.NextDouble() * 2 - 1) * mag);
            }
            else
            {
                Vfx.Shake = default;
            }

            bool keep = Vfx.HasActive || _shakeTime > 0;
            if (!keep) _decayRunning = false;
            return keep;
        });
    }
}

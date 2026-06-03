namespace MauiApp.Rendering;

/// <summary>缓动函数集合（输入/输出均为 0..1 归一化进度）。</summary>
public static class Easing
{
    public static float Linear(float t) => t;
    public static float OutQuad(float t) => 1 - (1 - t) * (1 - t);
    public static float InQuad(float t) => t * t;
    public static float InOutQuad(float t) => t < 0.5f ? 2 * t * t : 1 - MathF.Pow(-2 * t + 2, 2) / 2;
    public static float OutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
    public static float OutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
    }
}

/// <summary>
/// 一个标量补间：在 <see cref="Duration"/> 秒内从 <see cref="From"/> 过渡到 <see cref="To"/>。
/// 由 <see cref="AnimationClock"/> 每帧推进 <see cref="Advance"/>。
/// </summary>
public sealed class Tween
{
    private readonly Func<float, float> _easing;
    private float _elapsed;

    public Tween(float from, float to, float duration, Func<float, float>? easing = null)
    {
        From = from;
        To = to;
        Duration = MathF.Max(0.0001f, duration);
        _easing = easing ?? Easing.OutQuad;
    }

    public float From { get; }
    public float To { get; }
    public float Duration { get; }
    public bool IsDone => _elapsed >= Duration;

    /// <summary>当前进度对应的插值。</summary>
    public float Value
    {
        get
        {
            float t = Math.Clamp(_elapsed / Duration, 0f, 1f);
            return From + (To - From) * _easing(t);
        }
    }

    /// <summary>推进 dt 秒；返回是否仍在进行中。</summary>
    public bool Advance(float dt)
    {
        _elapsed += dt;
        return !IsDone;
    }
}

using MauiApp.Game.Battle;
using SkiaSharp;

namespace MauiApp.Rendering;

/// <summary>正在淡出的阵亡单位（引擎已判定死亡，UI 仍补放倒地动画）。</summary>
public sealed class DyingUnit
{
    public int Id { get; set; }
    public int Col { get; set; }
    public int Row { get; set; }
    public BattleSide Side { get; set; }
    public bool IsGeneral { get; set; }
    public float Alpha { get; set; } = 1f;
}

/// <summary>
/// 战斗瞬态视觉状态：单位像素偏移（滑动/冲刺）、受击闪白、飘字、阵亡淡出、震屏。
/// 由 <see cref="BattleAnimator"/> 写入，<see cref="BattleRenderer"/> 每帧读取。引擎逻辑零侵入。
/// </summary>
public sealed class BattleVfx
{
    public readonly Dictionary<int, SKPoint> Offset = new();
    public readonly Dictionary<int, float> Flash = new();
    public readonly List<FloatingText> Texts = new();
    public readonly List<DyingUnit> Dying = new();
    public SKPoint Shake;

    public SKPoint OffsetOf(int id) => Offset.TryGetValue(id, out var p) ? p : default;
    public float FlashOf(int id) => Flash.TryGetValue(id, out var f) ? f : 0f;

    public bool HasActive =>
        Offset.Values.Any(p => p != default) ||
        Flash.Values.Any(v => v > 0.01f) ||
        Texts.Count > 0 || Dying.Count > 0 || Shake != default;

    public void Clear()
    {
        Offset.Clear();
        Flash.Clear();
        Texts.Clear();
        Dying.Clear();
        Shake = default;
    }
}

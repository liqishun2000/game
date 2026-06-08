using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace MauiApp.Rendering;

/// <summary>
/// 地图触摸：SKTouch 单指拖拽/点击 + 双指捏合缩放（Android 上 PinchGesture 与 SKTouch 冲突）。
/// </summary>
public static class MapCanvasGestures
{
    private const float DragThresholdSq = 12f * 12f;

    public static void Attach(SKCanvasView canvas,
        Action<float, float> onPanDelta,
        Action<float, float> onTap,
        Action<float, float, float>? onPinchZoom = null)
    {
        canvas.InputTransparent = false;
        BindSkiaTouch(canvas, onPanDelta, onTap, onPinchZoom);
    }

    private static void BindSkiaTouch(SKCanvasView canvas,
        Action<float, float> onPanDelta,
        Action<float, float> onTap,
        Action<float, float, float>? onPinchZoom)
    {
        var points = new Dictionary<long, SKPoint>();
        var prevPoints = new Dictionary<long, SKPoint>();
        float lastPinchDist = 0f;
        long? primaryId = null;
        float startX = 0f, startY = 0f;
        bool dragging = false;

        canvas.EnableTouchEvents = true;
        canvas.Touch += (_, e) =>
        {
            e.Handled = true;

            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    points[e.Id] = e.Location;
                    prevPoints[e.Id] = e.Location;
                    if (points.Count == 1)
                    {
                        primaryId = e.Id;
                        startX = e.Location.X;
                        startY = e.Location.Y;
                        dragging = false;
                    }
                    if (points.Count == 2)
                    {
                        dragging = false;
                        lastPinchDist = PinchDistance(points);
                    }
                    break;

                case SKTouchAction.Moved:
                    if (!points.ContainsKey(e.Id)) break;
                    var prev = prevPoints[e.Id];
                    points[e.Id] = e.Location;
                    prevPoints[e.Id] = e.Location;

                    if (points.Count >= 2 && onPinchZoom is not null)
                    {
                        float dist = PinchDistance(points);
                        if (lastPinchDist > 1f)
                        {
                            float factor = dist / lastPinchDist;
                            if (Math.Abs(factor - 1f) > 0.001f)
                            {
                                var center = PinchCenter(points);
                                onPinchZoom(factor, center.X, center.Y);
                            }
                        }
                        lastPinchDist = dist;
                    }
                    else if (points.Count == 1 && primaryId == e.Id)
                    {
                        float x = e.Location.X, y = e.Location.Y;
                        if (!dragging)
                        {
                            float dx = x - startX, dy = y - startY;
                            if (dx * dx + dy * dy < DragThresholdSq) break;
                            dragging = true;
                        }
                        onPanDelta(x - prev.X, y - prev.Y);
                    }
                    break;

                case SKTouchAction.Released:
                case SKTouchAction.Cancelled:
                {
                    bool tap = points.Count == 1 && !dragging && primaryId == e.Id;
                    float tapX = e.Location.X, tapY = e.Location.Y;

                    points.Remove(e.Id);
                    prevPoints.Remove(e.Id);

                    if (points.Count < 2)
                        lastPinchDist = 0f;

                    if (points.Count == 1)
                    {
                        primaryId = points.Keys.First();
                        var p = points[primaryId.Value];
                        startX = p.X;
                        startY = p.Y;
                        dragging = false;
                    }
                    else
                        primaryId = null;

                    if (tap)
                        onTap(tapX, tapY);
                    dragging = false;
                    break;
                }
            }
        };
    }

    private static float PinchDistance(Dictionary<long, SKPoint> pts)
    {
        var arr = pts.Values.Take(2).ToArray();
        if (arr.Length < 2) return 0f;
        float dx = arr[1].X - arr[0].X, dy = arr[1].Y - arr[0].Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static SKPoint PinchCenter(Dictionary<long, SKPoint> pts)
    {
        var arr = pts.Values.Take(2).ToArray();
        if (arr.Length < 2) return arr.Length == 1 ? arr[0] : SKPoint.Empty;
        return new SKPoint((arr[0].X + arr[1].X) / 2f, (arr[0].Y + arr[1].Y) / 2f);
    }
}

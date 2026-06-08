namespace MauiApp.Rendering;

/// <summary>2D 地图相机：平移 + 缩放，用于大地图与战斗方格的拖拽浏览。</summary>
public sealed class MapCamera
{
    public const float DefaultMinZoom = 0.2f;
    public const float DefaultMaxZoom = 5f;

    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float Zoom { get; set; } = 1f;
    public float MinZoom { get; set; } = DefaultMinZoom;
    public float MaxZoom { get; set; } = DefaultMaxZoom;

    /// <summary>限制相机偏移，使内容不会露出空白边。</summary>
    public void Clamp(float viewportW, float viewportH, float contentW, float contentH)
    {
        float scaledW = contentW * Zoom;
        float scaledH = contentH * Zoom;

        float maxX = Math.Max(0, scaledW - viewportW);
        float maxY = Math.Max(0, scaledH - viewportH);

        OffsetX = Math.Clamp(OffsetX, 0, maxX);
        OffsetY = Math.Clamp(OffsetY, 0, maxY);
    }

    /// <summary>内容大于视口时居中显示。</summary>
    public void FitCenter(float viewportW, float viewportH, float contentW, float contentH)
    {
        float scaledW = contentW * Zoom;
        float scaledH = contentH * Zoom;
        OffsetX = Math.Max(0, (scaledW - viewportW) / 2f);
        OffsetY = Math.Max(0, (scaledH - viewportH) / 2f);
        Clamp(viewportW, viewportH, contentW, contentH);
    }

    /// <summary>缩放以适配矩形区域，并居中对准。</summary>
    public void FitToBounds(float viewportW, float viewportH,
        float boundsX, float boundsY, float boundsW, float boundsH,
        float contentW, float contentH, float margin = 1.1f)
    {
        if (boundsW <= 1f || boundsH <= 1f)
        {
            Zoom = Math.Clamp(Math.Min(viewportW / contentW, viewportH / contentH) * 0.92f, MinZoom, MaxZoom);
            FitCenter(viewportW, viewportH, contentW, contentH);
            return;
        }

        Zoom = Math.Clamp(
            Math.Min(viewportW / (boundsW * margin), viewportH / (boundsH * margin)),
            MinZoom, MaxZoom);
        FocusOnBounds(viewportW, viewportH, boundsX, boundsY, boundsW, boundsH, contentW, contentH);
    }

    /// <summary>将视口对准内容中的某一矩形区域（如节点群）。</summary>
    public void FocusOnBounds(float viewportW, float viewportH, float boundsX, float boundsY,
        float boundsW, float boundsH, float contentW, float contentH)
    {
        float cx = boundsX + boundsW / 2f;
        float cy = boundsY + boundsH / 2f;
        OffsetX = cx * Zoom - viewportW / 2f;
        OffsetY = cy * Zoom - viewportH / 2f;
        Clamp(viewportW, viewportH, contentW, contentH);
    }

    public (float X, float Y) ScreenToWorld(float screenX, float screenY) =>
        ((screenX + OffsetX) / Zoom, (screenY + OffsetY) / Zoom);

    /// <summary>以屏幕锚点缩放，保持锚点下世界坐标不变。</summary>
    public void ZoomAt(float screenX, float screenY, float zoomFactor,
        float viewportW, float viewportH, float contentW, float contentH)
    {
        if (Math.Abs(zoomFactor - 1f) < 0.001f) return;
        float wx = (screenX + OffsetX) / Zoom;
        float wy = (screenY + OffsetY) / Zoom;
        Zoom = Math.Clamp(Zoom * zoomFactor, MinZoom, MaxZoom);
        OffsetX = wx * Zoom - screenX;
        OffsetY = wy * Zoom - screenY;
        Clamp(viewportW, viewportH, contentW, contentH);
    }

    /// <summary>手指拖动：地图跟随手指（抓取拖动）。</summary>
    public void Pan(float dx, float dy)
    {
        OffsetX -= dx;
        OffsetY -= dy;
    }
}

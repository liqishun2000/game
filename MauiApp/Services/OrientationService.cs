namespace MauiApp.Services;

/// <summary>游戏内强制横屏。</summary>
public static class OrientationService
{
    public static void LockLandscape()
    {
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity is not null)
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape;
#elif IOS
        // Info.plist 已限制为横屏；此处可扩展 UIDevice 锁定
#endif
    }
}

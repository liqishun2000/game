using MauiApp.Rendering;

namespace MauiApp.Services;

/// <summary>进入/恢复沉浸式全屏（隐藏状态栏与导航栏）。</summary>
public static class ImmersiveService
{
    public static void Enable()
    {
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity?.Window is null) return;

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            activity.Window.SetDecorFitsSystemWindows(false);
            var controller = activity.Window.InsetsController;
            if (controller is not null)
            {
                controller.Hide(Android.Views.WindowInsets.Type.StatusBars()
                                | Android.Views.WindowInsets.Type.NavigationBars());
                controller.SystemBarsBehavior = (int)Android.Views.WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }
        else
        {
#pragma warning disable CS0618
            var flags = Android.Views.SystemUiFlags.ImmersiveSticky
                        | Android.Views.SystemUiFlags.Fullscreen
                        | Android.Views.SystemUiFlags.HideNavigation
                        | Android.Views.SystemUiFlags.LayoutStable
                        | Android.Views.SystemUiFlags.LayoutFullscreen
                        | Android.Views.SystemUiFlags.LayoutHideNavigation;
            activity.Window.DecorView.SystemUiFlags = flags;
#pragma warning restore CS0618
        }

        ApplyInsets(activity);
#endif
    }

#if ANDROID
    private static void ApplyInsets(Android.App.Activity activity)
    {
        var decor = activity.Window?.DecorView;
        if (decor is null) return;

        decor.SetOnApplyWindowInsetsListener(new InsetsListener());
        decor.RequestApplyInsets();
    }

    private sealed class InsetsListener : Java.Lang.Object, Android.Views.View.IOnApplyWindowInsetsListener
    {
        public Android.Views.WindowInsets OnApplyWindowInsets(Android.Views.View v, Android.Views.WindowInsets insets)
        {
            var bars = insets.GetInsets(Android.Views.WindowInsets.Type.SystemBars());
            SafeInsets.Top = bars.Top;
            SafeInsets.Bottom = bars.Bottom;
            return insets;
        }
    }
#endif
}

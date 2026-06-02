using Microsoft.Extensions.Logging;
using MauiAppHost = global::Microsoft.Maui.Hosting.MauiApp;

namespace MauiApp;

public static class MauiProgram
{
    public static MauiAppHost CreateMauiApp()
    {
        var builder = MauiAppHost.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
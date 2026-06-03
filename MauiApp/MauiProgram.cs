using MauiApp.Services;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MauiAppHost = global::Microsoft.Maui.Hosting.MauiApp;

namespace MauiApp;

public static class MauiProgram
{
    public static MauiAppHost CreateMauiApp()
    {
        var builder = MauiAppHost.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .AddAudio()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("zpix.ttf", "Pixel");
            });

        builder.Services.AddSingleton<AudioService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        ServiceHelper.Services = app.Services;
        return app;
    }
}

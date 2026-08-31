using Microsoft.Extensions.Logging;

namespace Urd;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        MauiAppBuilder builder = MauiApp
            .CreateBuilder()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
        ;

#if DEBUG
        builder.Logging.AddDebug();
#if ANDROID
        Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);
#endif
#endif

        return builder.Build();
    }
}

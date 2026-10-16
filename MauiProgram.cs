using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
#endif

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
            .UseMauiCommunityToolkit()
        ;

#if MAUI_DEVFLOW
        builder.AddMauiDevFlowAgent();
#endif

#if DEBUG
        builder.Logging.AddDebug();
#if ANDROID
        Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);
#endif
#endif

        return builder.Build();
    }
}

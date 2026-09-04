using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;

namespace Urd;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity {
    protected override async void OnCreate(Bundle? savedInstanceState) {
        base.OnCreate(savedInstanceState);

        RequestBatteryOptimizationExemption();
        RequestNotificationPermission();

        Global.StartService(typeof(FolderWatcherService), this);
    }

    void RequestBatteryOptimizationExemption() {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23)) return;

        PowerManager powerManager = (PowerManager)GetSystemService(PowerService)!;

        if (!powerManager.IsIgnoringBatteryOptimizations(PackageName)) {
            Intent intent = new(Settings.ActionRequestIgnoreBatteryOptimizations);
            intent.SetData(Android.Net.Uri.Parse($"package:{PackageName}"));
            StartActivity(intent);
        }
    }

    void RequestNotificationPermission() {
        // Not needed on API < 33
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;

        RequestPermissions([Manifest.Permission.PostNotifications], 100);
    }
}

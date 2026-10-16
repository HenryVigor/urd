using Android.App;
using Android.Content;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted])]
public class BootReceiver : BroadcastReceiver {
    public override void OnReceive(Context? context, Intent? intent) {
        if (intent?.Action != Intent.ActionBootCompleted) return;

        FolderWatcherService.Start();
    }
}

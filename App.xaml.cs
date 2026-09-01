namespace Urd;

public partial class App : Application {
    public App() {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState) {
        return new Window(new AppShell());
    }

    protected override void OnStart() {
        Api.LocalServer.Start();
    }

    protected override void OnSleep() {
        Api.LocalServer.Stop();
    }

    protected override void OnResume() {
        Api.LocalServer.Start();
    }
}

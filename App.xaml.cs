namespace Urd;

public partial class App : Application {
    public App() {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState) {
        Window window = new(new AppShell());
        window.Created += (_, _) => ApiServer.Start();
        window.Destroying += (_, _) => ApiServer.Stop();

        return window;
    }
}

namespace Urd;

public partial class MainPage : ContentPage {
    public MainPage() {
        InitializeComponent();

#if DEBUG
        RootGrid.Children.Add(new WebView { Source = "http://localhost:5173/" });
#else
        RootGrid.Children.Add(new HybridWebView
        {
            HybridRoot = "wwwroot",
            DefaultFile = "index.html"
        });
#endif
    }
}

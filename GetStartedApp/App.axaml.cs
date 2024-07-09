using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GetStartedApp.ViewModels;
using GetStartedApp.Views;

namespace GetStartedApp;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new Views.WelcomeWindow {
                DataContext = new ViewModels.WelcomeWindow(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

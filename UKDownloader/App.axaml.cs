using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using UKDownloader.ViewModels;

namespace UKDownloader;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var checkWindow = new CheckWindow();
            desktop.MainWindow = checkWindow;
            checkWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

}
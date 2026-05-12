using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.Views;

namespace Tianming.Desktop.Avalonia;

public partial class App : Application
{
    internal static IServiceProvider? Services { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = AppHost.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var lifecycle = Services.GetRequiredService<AppLifecycle>();
            _ = lifecycle.OnStartupAsync();

            var vm = Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = vm };

            var savedState = lifecycle.LoadInitialWindowState();
            window.Width  = savedState.Width;
            window.Height = savedState.Height;
            if (!double.IsNaN(savedState.X) && !double.IsNaN(savedState.Y))
                window.Position = new PixelPoint((int)savedState.X, (int)savedState.Y);
            if (savedState.IsMaximized)
                window.WindowState = global::Avalonia.Controls.WindowState.Maximized;

            window.Closing += (_, _) =>
            {
                var layout = Services!.GetRequiredService<ThreeColumnLayoutViewModel>();
                var state = new WindowState(
                    X: window.Position.X,
                    Y: window.Position.Y,
                    Width: window.Width,
                    Height: window.Height,
                    LeftColumnWidth: layout.LeftColumnWidth,
                    RightColumnWidth: layout.RightColumnWidth,
                    IsMaximized: window.WindowState == global::Avalonia.Controls.WindowState.Maximized);
                lifecycle.SaveWindowState(state);
            };

            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}

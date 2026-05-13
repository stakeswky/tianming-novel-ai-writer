using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
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
        // 1. LiveCharts2 全局主题：颜色 palette + 中文字体
        ConfigureLiveCharts();

        // 2. AvaloniaEdit 字体 / 高亮初始化
        AvaloniaEditBootstrap.Initialize();

        Services = AppHost.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var lifecycle = Services.GetRequiredService<AppLifecycle>();
            _ = lifecycle.OnStartupAsync();

            var vm = Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = vm };
            _ = vm.StatusBar.RefreshProbesAsync();

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

    /// <summary>
    /// 启动时配置 LiveCharts2 全局主题：5 个 palette 颜色 + PingFang SC 中文字体。
    /// LiveCharts 2.0.2 正确入口：AddLightTheme(Action&lt;Theme&gt;)（base theme 含所有 builder
    /// 实装，再用 callback 覆盖 palette）。早先 HasTheme(Action&lt;Theme&gt;) 是空白 Theme，
    /// 内部 builder lambda 默认 throw NotImplementedException，chart load 时崩溃。
    /// </summary>
    private static void ConfigureLiveCharts()
    {
        var typeface = SKFontManager.Default.MatchFamily("PingFang SC") ?? SKTypeface.Default;

        LiveCharts.Configure(settings => settings
            .AddSkiaSharp()
            .AddDefaultMappers()
            .HasTextSettings(new TextSettings { DefaultTypeface = typeface })
            .AddLightTheme(theme =>
            {
                theme.Colors = new[]
                {
                    new LvcColor(6, 182, 212),      // AccentBase #06B6D4
                    new LvcColor(16, 185, 129),     // StatusSuccess #10B981
                    new LvcColor(245, 158, 11),     // StatusWarning #F59E0B
                    new LvcColor(59, 130, 246),     // StatusInfo #3B82F6
                    new LvcColor(148, 163, 184),    // StatusNeutral #94A3B8
                };
            }));
    }
}

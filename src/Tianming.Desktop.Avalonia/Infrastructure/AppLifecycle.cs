using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Theme;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class AppLifecycle
{
    private readonly AppPaths _paths;
    private readonly WindowStateStore _windowStore;
    private readonly ThemeBridge _theme;
    private readonly INavigationService _nav;
    private readonly ILogger<AppLifecycle> _log;

    public AppLifecycle(
        AppPaths paths,
        WindowStateStore windowStore,
        ThemeBridge theme,
        INavigationService nav,
        ILogger<AppLifecycle> log)
    {
        _paths = paths;
        _windowStore = windowStore;
        _theme = theme;
        _nav = nav;
        _log = log;
    }

    public async Task OnStartupAsync()
    {
        _paths.EnsureDirectories();
        _log.LogInformation("AppSupport={Path}", _paths.AppSupportDirectory);
        await _theme.InitializeAsync();
        await _nav.NavigateAsync(PageKeys.Welcome);
    }

    public WindowState LoadInitialWindowState() => _windowStore.Load();

    public void SaveWindowState(WindowState state) => _windowStore.Save(state);

    public Task OnShutdownAsync()
    {
        _log.LogInformation("Shutting down");
        return Task.CompletedTask;
    }
}

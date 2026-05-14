using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using TM.Framework.Appearance;

namespace Tianming.Desktop.Avalonia.Theme;

public sealed class ThemeBridge
{
    private readonly ILogger<ThemeBridge> _log;

    public ThemeBridge(ILogger<ThemeBridge> log) { _log = log; }

    public Task InitializeAsync()
    {
        ApplyLightDarkVariant(PortableThemeType.Light);
        return Task.CompletedTask;
    }

    /// <summary>Callback wired to PortableThemeStateController.</summary>
    public Task ApplyAsync(PortableThemeApplicationRequest request)
    {
        if (Dispatcher.UIThread.CheckAccess())
            ApplyCore(request);
        else
            Dispatcher.UIThread.Post(() => ApplyCore(request));
        return Task.CompletedTask;
    }

    private void ApplyCore(PortableThemeApplicationRequest request)
    {
        var app = Application.Current;
        if (app is null) return;

        foreach (var kv in request.Brushes)
        {
            if (!TryParseHex(kv.Value, out var color)) continue;
            app.Resources[kv.Key] = new SolidColorBrush(color);
        }

        app.RequestedThemeVariant = ToAvaloniaVariant(request.Plan.ColorMode);
        _log.LogInformation("Applied theme {Theme} ({Variant})", request.Plan.ThemeType, app.RequestedThemeVariant);
    }

    private void ApplyLightDarkVariant(PortableThemeType type)
    {
        var app = Application.Current;
        if (app is null) return;
        var plan = PortableThemeApplicationPlanner.CreateBuiltInPlan(type);
        app.RequestedThemeVariant = ToAvaloniaVariant(plan.ColorMode);
    }

    private static ThemeVariant ToAvaloniaVariant(PortableThemeColorMode colorMode)
        => colorMode == PortableThemeColorMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

    private static bool TryParseHex(string s, out Color color)
    {
        if (Color.TryParse(s, out color)) return true;
        color = default;
        return false;
    }
}

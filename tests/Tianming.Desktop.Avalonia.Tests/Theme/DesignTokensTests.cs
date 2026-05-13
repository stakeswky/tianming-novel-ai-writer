using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Theme;

public class DesignTokensTests
{
    private static readonly string ColorsXamlUri = "avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml";

    private static int _appInitialized;

    private static void EnsureAvaloniaInitialized()
    {
        if (Interlocked.Exchange(ref _appInitialized, 1) == 1) return;
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
    }

    private static IResourceDictionary LoadColors()
    {
        // 通过 ResourceInclude 加载 axaml 文件；需要 Application bootstrap 提供 IAssetLoader
        EnsureAvaloniaInitialized();
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri(ColorsXamlUri)
        };
        return include.Loaded;
    }

    [Fact]
    public void Colors_AccentKeysResolve()
    {
        var dict = LoadColors();
        var keys = new[] { "AccentBase", "AccentHover", "AccentPressed", "AccentSubtle", "AccentForeground" };
        foreach (var k in keys)
        {
            Assert.True(dict.TryGetResource(k, null, out var v), $"missing key {k}");
            Assert.IsType<Color>(v);
        }
    }

    [Fact]
    public void Colors_NeutralAndTextKeysResolve()
    {
        var dict = LoadColors();
        var keys = new[]
        {
            "SurfaceBase", "SurfaceCanvas", "SurfaceSubtle", "SurfaceMuted",
            "BorderSubtle", "BorderStrong",
            "TextPrimary", "TextSecondary", "TextTertiary", "TextOnAccent"
        };
        foreach (var k in keys)
        {
            Assert.True(dict.TryGetResource(k, null, out var v), $"missing key {k}");
            Assert.IsType<Color>(v);
        }
    }

    [Fact]
    public void Colors_StatusKeysResolve()
    {
        var dict = LoadColors();
        var keys = new[]
        {
            "StatusSuccess", "StatusSuccessSubtle",
            "StatusWarning", "StatusWarningSubtle",
            "StatusDanger",  "StatusDangerSubtle",
            "StatusInfo",    "StatusInfoSubtle",
            "StatusNeutral", "StatusNeutralSubtle"
        };
        foreach (var k in keys)
        {
            Assert.True(dict.TryGetResource(k, null, out var v), $"missing key {k}");
            Assert.IsType<Color>(v);
        }
    }

    [Fact]
    public void Colors_BrushAliasesResolve()
    {
        var dict = LoadColors();
        // 每个 Color 都有同名 *Brush 别名
        Assert.True(dict.TryGetResource("AccentBaseBrush", null, out var v));
        Assert.IsType<SolidColorBrush>(v);
        Assert.True(dict.TryGetResource("TextPrimaryBrush", null, out v));
        Assert.IsType<SolidColorBrush>(v);
        Assert.True(dict.TryGetResource("StatusSuccessBrush", null, out v));
        Assert.IsType<SolidColorBrush>(v);
    }
}

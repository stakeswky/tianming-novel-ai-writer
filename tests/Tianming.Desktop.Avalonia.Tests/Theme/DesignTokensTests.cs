using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Theme;

public class DesignTokensTests
{
    // 注：以前用 [Fact] + 手动 AppBuilder.Configure().UseHeadless().SetupWithoutStarting()，
    // 自从 AssemblyInfo 注册 AvaloniaTestApplication(TestApp) 之后，
    // 测试运行时 Avalonia headless 已经初始化好；改用 [AvaloniaFact] 让 Avalonia.Headless.XUnit
    // 在 UI 线程上跑测试，避免 "Call from invalid thread"。
    private static readonly string ColorsXamlUri = "avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml";

    private static IResourceDictionary LoadColors()
    {
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri(ColorsXamlUri)
        };
        return include.Loaded;
    }

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    private static IResourceDictionary LoadTypography()
    {
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri("avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Typography.axaml")
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Typography_FontFamiliesResolve()
    {
        var dict = LoadTypography();
        Assert.True(dict.TryGetResource("FontUI", null, out var ui));
        Assert.IsType<FontFamily>(ui);
        Assert.True(dict.TryGetResource("FontMono", null, out var mono));
        Assert.IsType<FontFamily>(mono);
    }

    [AvaloniaFact]
    public void Typography_FontSizesResolveAndAreNumeric()
    {
        var dict = LoadTypography();
        var sizes = new[] { "FontSizeDisplay", "FontSizeH1", "FontSizeH2", "FontSizeH3",
                            "FontSizeBody", "FontSizeSecondary", "FontSizeCaption" };
        foreach (var k in sizes)
        {
            Assert.True(dict.TryGetResource(k, null, out var v), $"missing {k}");
            Assert.IsType<double>(v);
            Assert.True((double)v! > 0);
        }
    }

    [AvaloniaFact]
    public void Typography_WeightsAndLineHeightsResolve()
    {
        var dict = LoadTypography();
        foreach (var k in new[] { "FontWeightRegular", "FontWeightMedium", "FontWeightSemibold", "FontWeightBold" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<FontWeight>(v);
        }
        foreach (var k in new[] { "LineHeightTight", "LineHeightNormal", "LineHeightRelaxed" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<double>(v);
        }
    }

    private static IResourceDictionary LoadSpacing()
    {
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri("avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Spacing.axaml")
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Spacing_AllSpaceKeysResolveAndIncreasing()
    {
        var dict = LoadSpacing();
        var keys = new[] { "Space1", "Space2", "Space3", "Space4", "Space5", "Space6", "Space8", "Space10" };
        double prev = 0;
        foreach (var k in keys)
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            var val = Assert.IsType<double>(v);
            Assert.True(val > prev, $"{k} should be > previous {prev}, got {val}");
            prev = val;
        }
    }

    [AvaloniaFact]
    public void Spacing_PaddingThicknessKeysResolve()
    {
        var dict = LoadSpacing();
        foreach (var k in new[] { "PaddingCard", "PaddingPage", "PaddingInputControl" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<Thickness>(v);
        }
    }

    private static IResourceDictionary LoadRadii()
    {
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri("avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Radii.axaml")
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Radii_AllKeysResolveAsCornerRadius()
    {
        var dict = LoadRadii();
        foreach (var k in new[] { "RadiusSm", "RadiusMd", "RadiusLg", "RadiusXl", "RadiusFull" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<CornerRadius>(v);
        }
    }

    private static IResourceDictionary LoadShadows()
    {
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri("avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Shadows.axaml")
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Shadows_AllKeysResolveAsBoxShadows()
    {
        var dict = LoadShadows();
        foreach (var k in new[] { "ShadowSm", "ShadowMd", "ShadowLg" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<BoxShadows>(v);
        }
    }
}

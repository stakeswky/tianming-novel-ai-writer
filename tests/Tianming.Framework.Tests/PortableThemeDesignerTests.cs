using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeDesignerTests
{
    [Fact]
    public void GenerateThemeXaml_maps_designer_colors_to_original_theme_keys()
    {
        var snapshot = PortableThemeDesignSnapshot.CreateDefault();
        snapshot.ThemeName = "Mac Theme";
        snapshot.TopBarBackground = "#112233";
        snapshot.CenterWorkspaceBackground = "#445566";
        snapshot.LeftBarBackground = "#000000";
        snapshot.LeftWorkspaceBackground = "#000000";
        snapshot.CenterWorkspaceText = "#FFFFFF";
        snapshot.PrimaryButtonColor = "#336699";
        snapshot.PrimaryButtonHover = "#6699CC";
        snapshot.DangerButtonColor = "#CC3333";
        snapshot.DangerButtonHover = "#AA2222";

        var xaml = PortableThemeDesigner.GenerateThemeXaml(snapshot);

        Assert.Contains("<!-- Mac Theme -->", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"UnifiedBackground\" Color=\"#112233\"/>", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"ContentBackground\" Color=\"#445566\"/>", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"TextPrimary\" Color=\"#FFFFFF\"/>", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"TextDisabled\" Color=\"#7F7F7F\"/>", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"HoverBackground\" Color=\"#333333\"/>", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"ActiveBackground\" Color=\"#4C4C4C\"/>", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"PrimaryActive\" Color=\"#517AA3\"/>", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"DangerColor\" Color=\"#CC3333\"/>", xaml);
    }

    [Fact]
    public async Task SaveThemeAsync_sanitizes_file_name_and_writes_theme_atomically()
    {
        using var workspace = new TempDirectory();
        var snapshot = PortableThemeDesignSnapshot.CreateDefault();
        snapshot.ThemeName = "My/Theme";

        var result = await PortableThemeDesigner.SaveThemeAsync(
            workspace.Path,
            snapshot,
            _ => true);

        Assert.True(result.Success);
        Assert.Equal("MyThemeTheme.xaml", result.FileName);
        Assert.True(File.Exists(Path.Combine(workspace.Path, "MyThemeTheme.xaml")));
        Assert.False(File.Exists(Path.Combine(workspace.Path, "MyThemeTheme.xaml.tmp")));
    }

    [Fact]
    public async Task SaveThemeAsync_respects_overwrite_policy()
    {
        using var workspace = new TempDirectory();
        var existingPath = Path.Combine(workspace.Path, "ExistingTheme.xaml");
        await File.WriteAllTextAsync(existingPath, "old");
        var snapshot = PortableThemeDesignSnapshot.CreateDefault();
        snapshot.ThemeName = "Existing";

        var skipped = await PortableThemeDesigner.SaveThemeAsync(
            workspace.Path,
            snapshot,
            _ => false);

        Assert.False(skipped.Success);
        Assert.Equal("old", await File.ReadAllTextAsync(existingPath));
    }

    [Fact]
    public void AnalyzeContrast_returns_original_warning_levels_for_low_contrast_pairs()
    {
        var snapshot = PortableThemeDesignSnapshot.CreateDefault();
        snapshot.TopBarBackground = "#FFFFFF";
        snapshot.TopBarText = "#FFFFFF";
        snapshot.CenterWorkspaceBackground = "#FFFFFF";
        snapshot.CenterWorkspaceText = "#777777";

        var warnings = PortableThemeDesigner.AnalyzeContrast(snapshot);

        Assert.Contains(warnings, warning => warning.Area == "top" && warning.Level == "critical");
        Assert.Contains(warnings, warning => warning.Area == "center" && warning.Level == "warning");
        Assert.DoesNotContain(warnings, warning => warning.Area == "left");
    }

    [Fact]
    public void CalculateContrastRatio_matches_wcag_black_white_ratio()
    {
        var ratio = PortableThemeDesigner.CalculateContrastRatio("#000000", "#FFFFFF");

        Assert.Equal(21.0, ratio, precision: 1);
    }
}

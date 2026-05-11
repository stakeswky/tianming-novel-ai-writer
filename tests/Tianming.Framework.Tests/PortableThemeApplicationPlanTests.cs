using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeApplicationPlanTests
{
    [Theory]
    [InlineData(PortableThemeType.Light, "LightTheme.xaml", "浅色主题", PortableThemeColorMode.Light, "#3B82F6", "#FFFFFF")]
    [InlineData(PortableThemeType.Dark, "DarkTheme.xaml", "深色主题", PortableThemeColorMode.Dark, "#60A5FA", "#1E293B")]
    [InlineData(PortableThemeType.ModernBlue, "ModernBlueTheme.xaml", "现代深蓝", PortableThemeColorMode.Dark, "#1890FF", "#112240")]
    [InlineData(PortableThemeType.MinimalBlack, "MinimalBlackTheme.xaml", "极简黑", PortableThemeColorMode.Dark, "#6CB6FF", "#1A1A1A")]
    [InlineData(PortableThemeType.HighContrast, "HighContrastTheme.xaml", "高对比度", PortableThemeColorMode.Dark, "#FFFF00", "#000000")]
    public void CreateBuiltInPlan_maps_original_theme_metadata(
        PortableThemeType theme,
        string resourceFileName,
        string displayName,
        PortableThemeColorMode colorMode,
        string primaryColor,
        string backgroundColor)
    {
        var plan = PortableThemeApplicationPlanner.CreateBuiltInPlan(theme);

        Assert.Equal(theme, plan.ThemeType);
        Assert.Equal(resourceFileName, plan.ResourceFileName);
        Assert.Equal($"Appearance/ThemeManagement/Themes/{resourceFileName}", plan.ResourcePath);
        Assert.Equal(displayName, plan.DisplayName);
        Assert.Equal(colorMode, plan.ColorMode);
        Assert.Equal(primaryColor, plan.PrimaryColor);
        Assert.Equal(backgroundColor, plan.BackgroundColor);
        Assert.False(plan.IsCustom);
    }

    [Theory]
    [InlineData("DarkTheme.xaml", PortableThemeType.Dark)]
    [InlineData("ModernBlue", PortableThemeType.ModernBlue)]
    [InlineData("minimalblacktheme", PortableThemeType.MinimalBlack)]
    [InlineData("浅色主题", PortableThemeType.Light)]
    [InlineData("高对比度", PortableThemeType.HighContrast)]
    public void TryParseTheme_accepts_original_names_file_names_and_display_names(
        string value,
        PortableThemeType expected)
    {
        Assert.True(PortableThemeApplicationPlanner.TryParseTheme(value, out var theme));
        Assert.Equal(expected, theme);
    }

    [Theory]
    [InlineData(true, PortableThemeType.Light)]
    [InlineData(false, PortableThemeType.Dark)]
    public void CreateBuiltInPlan_resolves_auto_from_system_snapshot(
        bool isLightTheme,
        PortableThemeType expected)
    {
        var plan = PortableThemeApplicationPlanner.CreateBuiltInPlan(
            PortableThemeType.Auto,
            new PortableSystemThemeSnapshot(isLightTheme, false, null));

        Assert.Equal(expected, plan.ThemeType);
        Assert.Equal(expected == PortableThemeType.Light ? "LightTheme.xaml" : "DarkTheme.xaml", plan.ResourceFileName);
    }

    [Fact]
    public void CreateCustomPlan_normalizes_file_name_and_marks_custom()
    {
        var plan = PortableThemeApplicationPlanner.CreateCustomPlan("custom/midnight");

        Assert.Equal(PortableThemeType.Custom, plan.ThemeType);
        Assert.Equal("midnight.xaml", plan.ResourceFileName);
        Assert.Equal("Appearance/ThemeManagement/Themes/midnight.xaml", plan.ResourcePath);
        Assert.Equal("自定义主题", plan.DisplayName);
        Assert.True(plan.IsCustom);
    }

    [Fact]
    public void GetAllBuiltInPlans_excludes_auto_and_custom_but_covers_all_original_builtins()
    {
        var plans = PortableThemeApplicationPlanner.GetAllBuiltInPlans();

        Assert.DoesNotContain(plans, plan => plan.ThemeType is PortableThemeType.Auto or PortableThemeType.Custom);
        Assert.Contains(plans, plan => plan.ThemeType == PortableThemeType.Morandi);
        Assert.Contains(plans, plan => plan.ThemeType == PortableThemeType.Sunset);
        Assert.Equal(15, plans.Count);
        Assert.All(plans, plan => Assert.EndsWith("Theme.xaml", plan.ResourceFileName));
    }
}

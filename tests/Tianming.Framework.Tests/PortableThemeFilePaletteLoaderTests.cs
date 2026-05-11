using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeFilePaletteLoaderTests
{
    [Fact]
    public async Task LoadAsync_extracts_brushes_from_original_resource_dictionary_xaml()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Midnight.xaml");
        await File.WriteAllTextAsync(
            path,
            """
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <SolidColorBrush x:Key="PrimaryColor" Color="#123456"/>
                <SolidColorBrush x:Key="ContentBackground" Color="#0A0B0C"/>
                <SolidColorBrush x:Key="TextPrimary" Color="#F0F1F2"/>
                <SolidColorBrush x:Key="DangerColor" Color="#AA0000"/>
            </ResourceDictionary>
            """);

        var result = await PortableThemeFilePaletteLoader.LoadAsync(path);

        Assert.True(result.Success);
        Assert.Equal("Midnight.xaml", result.FileName);
        Assert.Equal("#123456", result.Brushes["PrimaryColor"]);
        Assert.Equal("#0A0B0C", result.Brushes["ContentBackground"]);
        Assert.Equal("#F0F1F2", result.Brushes["TextPrimary"]);
        Assert.Equal("#AA0000", result.Brushes["DangerColor"]);
    }

    [Fact]
    public async Task LoadAsync_normalizes_lowercase_and_ignores_unknown_brushes()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Theme.xaml");
        await File.WriteAllTextAsync(
            path,
            """
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <SolidColorBrush x:Key="PrimaryColor" Color="#abcdef"/>
                <SolidColorBrush x:Key="ContentBackground" Color="#010203"/>
                <SolidColorBrush x:Key="TextPrimary" Color="#AABBCC"/>
                <SolidColorBrush x:Key="UnknownColor" Color="#000000"/>
            </ResourceDictionary>
            """);

        var result = await PortableThemeFilePaletteLoader.LoadAsync(path);

        Assert.True(result.Success);
        Assert.Equal("#ABCDEF", result.Brushes["PrimaryColor"]);
        Assert.False(result.Brushes.ContainsKey("UnknownColor"));
    }

    [Theory]
    [InlineData("<NotATheme />", "ResourceDictionary")]
    [InlineData("""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <SolidColorBrush x:Key="PrimaryColor" Color="#123456"/>
        </ResourceDictionary>
        """, "required")]
    public async Task LoadAsync_rejects_invalid_or_incomplete_theme_files(string content, string expectedError)
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Invalid.xaml");
        await File.WriteAllTextAsync(path, content);

        var result = await PortableThemeFilePaletteLoader.LoadAsync(path);

        Assert.False(result.Success);
        Assert.Contains(expectedError, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Brushes);
    }

    [Fact]
    public async Task ThemeStateController_loads_custom_file_brushes_before_applying()
    {
        using var workspace = new TempDirectory();
        var themesDirectory = Path.Combine(workspace.Path, "themes");
        Directory.CreateDirectory(themesDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(themesDirectory, "Midnight.xaml"),
            """
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <SolidColorBrush x:Key="PrimaryColor" Color="#123456"/>
                <SolidColorBrush x:Key="ContentBackground" Color="#0A0B0C"/>
                <SolidColorBrush x:Key="TextPrimary" Color="#F0F1F2"/>
            </ResourceDictionary>
            """);
        var applied = new List<PortableThemeApplicationRequest>();
        var controller = new PortableThemeStateController(
            new PortableThemeState(),
            request =>
            {
                applied.Add(request);
                return Task.CompletedTask;
            },
            customThemeDirectory: themesDirectory);

        var result = await controller.ApplyThemeFromFileAsync("Midnight.xaml");

        Assert.True(result.Applied);
        var request = Assert.Single(applied);
        Assert.Equal("#123456", request.Brushes["PrimaryColor"]);
        Assert.Equal("#0A0B0C", request.Brushes["ContentBackground"]);
        Assert.Equal("#F0F1F2", request.Brushes["TextPrimary"]);
    }

    [Fact]
    public async Task ThemeStateController_returns_failure_when_custom_file_palette_cannot_load()
    {
        using var workspace = new TempDirectory();
        var themesDirectory = Path.Combine(workspace.Path, "themes");
        Directory.CreateDirectory(themesDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(themesDirectory, "Invalid.xaml"),
            """
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <SolidColorBrush x:Key="PrimaryColor" Color="#123456"/>
            </ResourceDictionary>
            """);
        var applyCount = 0;
        var saveCount = 0;
        var controller = new PortableThemeStateController(
            new PortableThemeState(),
            _ =>
            {
                applyCount++;
                return Task.CompletedTask;
            },
            (_, _) =>
            {
                saveCount++;
                return Task.CompletedTask;
            },
            customThemeDirectory: themesDirectory);

        var result = await controller.ApplyThemeFromFileAsync("Invalid.xaml");

        Assert.False(result.Applied);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PortableThemeType.Light, controller.CurrentTheme);
        Assert.Null(controller.CurrentThemeFileName);
        Assert.Equal(0, applyCount);
        Assert.Equal(0, saveCount);
    }
}

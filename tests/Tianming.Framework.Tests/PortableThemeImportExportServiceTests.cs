using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeImportExportServiceTests
{
    [Fact]
    public async Task ImportThemesAsync_copies_valid_theme_and_skips_invalid_files()
    {
        using var workspace = new TempDirectory();
        var sourceDir = Path.Combine(workspace.Path, "source");
        var themesDir = Path.Combine(workspace.Path, "themes");
        Directory.CreateDirectory(sourceDir);
        var validTheme = Path.Combine(sourceDir, "ValidTheme.xaml");
        var invalidTheme = Path.Combine(sourceDir, "InvalidTheme.xaml");
        await File.WriteAllTextAsync(validTheme, ValidThemeXaml("Valid"));
        await File.WriteAllTextAsync(invalidTheme, "<NotATheme />");
        var service = new PortableThemeImportExportService(themesDir, Path.Combine(workspace.Path, "exports"));

        var result = await service.ImportThemesAsync([validTheme, invalidTheme]);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.True(File.Exists(Path.Combine(themesDir, "ValidTheme.xaml")));
        Assert.False(File.Exists(Path.Combine(themesDir, "InvalidTheme.xaml")));
    }

    [Fact]
    public async Task ImportThemesAsync_respects_overwrite_policy_for_existing_theme()
    {
        using var workspace = new TempDirectory();
        var sourceDir = Path.Combine(workspace.Path, "source");
        var themesDir = Path.Combine(workspace.Path, "themes");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(themesDir);
        var sourceTheme = Path.Combine(sourceDir, "Theme.xaml");
        var existingTheme = Path.Combine(themesDir, "Theme.xaml");
        await File.WriteAllTextAsync(sourceTheme, ValidThemeXaml("New"));
        await File.WriteAllTextAsync(existingTheme, ValidThemeXaml("Old"));
        var service = new PortableThemeImportExportService(themesDir, Path.Combine(workspace.Path, "exports"));

        var skipResult = await service.ImportThemesAsync([sourceTheme], _ => false);
        var replaceResult = await service.ImportThemesAsync([sourceTheme], _ => true);

        Assert.Equal(0, skipResult.ImportedCount);
        Assert.Equal(1, skipResult.SkippedCount);
        Assert.Equal(1, replaceResult.ImportedCount);
        Assert.Contains("New", await File.ReadAllTextAsync(existingTheme));
    }

    [Fact]
    public async Task ExportCurrentThemeAsync_copies_theme_with_timestamped_name()
    {
        using var workspace = new TempDirectory();
        var themesDir = Path.Combine(workspace.Path, "themes");
        var exportDir = Path.Combine(workspace.Path, "exports");
        Directory.CreateDirectory(themesDir);
        await File.WriteAllTextAsync(Path.Combine(themesDir, "DarkTheme.xaml"), ValidThemeXaml("Dark"));
        var service = new PortableThemeImportExportService(
            themesDir,
            exportDir,
            () => new DateTime(2026, 5, 11, 14, 30, 5));

        var result = await service.ExportCurrentThemeAsync(PortableThemeType.Dark);

        Assert.True(result.Success);
        Assert.Equal("DarkTheme_20260511_143005.xaml", result.FileName);
        Assert.Equal(
            await File.ReadAllTextAsync(Path.Combine(themesDir, "DarkTheme.xaml")),
            await File.ReadAllTextAsync(Path.Combine(exportDir, result.FileName!)));
    }

    [Fact]
    public async Task ExportAllThemesAsync_copies_all_theme_files_to_timestamped_folder()
    {
        using var workspace = new TempDirectory();
        var themesDir = Path.Combine(workspace.Path, "themes");
        var exportDir = Path.Combine(workspace.Path, "exports");
        Directory.CreateDirectory(themesDir);
        await File.WriteAllTextAsync(Path.Combine(themesDir, "LightTheme.xaml"), ValidThemeXaml("Light"));
        await File.WriteAllTextAsync(Path.Combine(themesDir, "DarkTheme.xaml"), ValidThemeXaml("Dark"));
        await File.WriteAllTextAsync(Path.Combine(themesDir, "notes.txt"), "ignore me");
        var service = new PortableThemeImportExportService(
            themesDir,
            exportDir,
            () => new DateTime(2026, 5, 11, 14, 30, 5));

        var result = await service.ExportAllThemesAsync();

        Assert.Equal(2, result.ExportedCount);
        Assert.Equal("所有主题_20260511_143005", result.FolderName);
        Assert.True(File.Exists(Path.Combine(exportDir, result.FolderName!, "LightTheme.xaml")));
        Assert.True(File.Exists(Path.Combine(exportDir, result.FolderName!, "DarkTheme.xaml")));
        Assert.False(File.Exists(Path.Combine(exportDir, result.FolderName!, "notes.txt")));
    }

    [Fact]
    public async Task ListExportedThemesAsync_returns_newest_twenty_with_relative_paths_and_sizes()
    {
        using var workspace = new TempDirectory();
        var exportDir = Path.Combine(workspace.Path, "exports");
        Directory.CreateDirectory(exportDir);
        for (var i = 0; i < 22; i++)
        {
            var path = Path.Combine(exportDir, $"Theme-{i:00}.xaml");
            await File.WriteAllTextAsync(path, ValidThemeXaml(i.ToString()));
            File.SetCreationTime(path, new DateTime(2026, 5, 11, 12, i, 0));
        }
        var service = new PortableThemeImportExportService(Path.Combine(workspace.Path, "themes"), exportDir);

        var exported = await service.ListExportedThemesAsync();

        Assert.Equal(20, exported.Count);
        Assert.Equal("Theme-21.xaml", exported[0].FileName);
        Assert.Equal("Theme-02.xaml", exported[^1].FileName);
        Assert.EndsWith("B", exported[0].FileSize);
    }

    private static string ValidThemeXaml(string marker)
    {
        return $$"""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- {{marker}} -->
    <SolidColorBrush x:Key="PrimaryColor" Color="#3B82F6"/>
    <SolidColorBrush x:Key="ContentBackground" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="TextPrimary" Color="#111827"/>
</ResourceDictionary>
""";
    }
}

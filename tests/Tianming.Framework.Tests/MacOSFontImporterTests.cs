using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class MacOSFontImporterTests
{
    [Fact]
    public async Task ImportAsync_copies_supported_font_to_user_font_directory_and_reports_family_name()
    {
        using var workspace = new TempDirectory();
        var sourcePath = Path.Combine(workspace.Path, "Source-Han-Serif-Bold.ttf");
        var targetDirectory = Path.Combine(workspace.Path, "Library", "Fonts");
        await File.WriteAllTextAsync(sourcePath, "font payload");
        var importer = new MacOSFontImporter(targetDirectory);

        var result = await importer.ImportAsync(sourcePath);

        Assert.True(result.Imported);
        Assert.Equal("Source Han Serif", result.FontFamilyName);
        Assert.Equal(Path.Combine(targetDirectory, "Source-Han-Serif-Bold.ttf"), result.DestinationPath);
        Assert.Equal("font payload", await File.ReadAllTextAsync(result.DestinationPath));
    }

    [Fact]
    public async Task ImportAsync_creates_target_directory_when_missing()
    {
        using var workspace = new TempDirectory();
        var sourcePath = Path.Combine(workspace.Path, "Menlo-Regular.otf");
        var targetDirectory = Path.Combine(workspace.Path, "missing", "Fonts");
        await File.WriteAllTextAsync(sourcePath, "menlo");
        var importer = new MacOSFontImporter(targetDirectory);

        var result = await importer.ImportAsync(sourcePath);

        Assert.True(result.Imported);
        Assert.True(Directory.Exists(targetDirectory));
        Assert.True(File.Exists(result.DestinationPath));
    }

    [Fact]
    public async Task ImportAsync_uses_numbered_destination_when_font_file_already_exists()
    {
        using var workspace = new TempDirectory();
        var sourcePath = Path.Combine(workspace.Path, "Mono-Regular.ttc");
        var targetDirectory = Path.Combine(workspace.Path, "Fonts");
        Directory.CreateDirectory(targetDirectory);
        await File.WriteAllTextAsync(sourcePath, "new");
        await File.WriteAllTextAsync(Path.Combine(targetDirectory, "Mono-Regular.ttc"), "existing");
        var importer = new MacOSFontImporter(targetDirectory);

        var result = await importer.ImportAsync(sourcePath);

        Assert.True(result.Imported);
        Assert.Equal(Path.Combine(targetDirectory, "Mono-Regular 1.ttc"), result.DestinationPath);
        Assert.Equal("existing", await File.ReadAllTextAsync(Path.Combine(targetDirectory, "Mono-Regular.ttc")));
        Assert.Equal("new", await File.ReadAllTextAsync(result.DestinationPath));
    }

    [Fact]
    public async Task ImportAsync_rejects_missing_source_file()
    {
        using var workspace = new TempDirectory();
        var importer = new MacOSFontImporter(Path.Combine(workspace.Path, "Fonts"));

        var result = await importer.ImportAsync(Path.Combine(workspace.Path, "missing.ttf"));

        Assert.False(result.Imported);
        Assert.Equal(MacOSFontImportFailureReason.SourceMissing, result.FailureReason);
        Assert.Equal(string.Empty, result.DestinationPath);
    }

    [Fact]
    public async Task ImportAsync_rejects_unsupported_file_extensions_without_copying()
    {
        using var workspace = new TempDirectory();
        var sourcePath = Path.Combine(workspace.Path, "readme.txt");
        var targetDirectory = Path.Combine(workspace.Path, "Fonts");
        await File.WriteAllTextAsync(sourcePath, "not a font");
        var importer = new MacOSFontImporter(targetDirectory);

        var result = await importer.ImportAsync(sourcePath);

        Assert.False(result.Imported);
        Assert.Equal(MacOSFontImportFailureReason.UnsupportedExtension, result.FailureReason);
        Assert.False(Directory.Exists(targetDirectory));
    }
}

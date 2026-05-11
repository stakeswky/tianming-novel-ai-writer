using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSoundLibraryTests
{
    [Fact]
    public void ListSounds_returns_supported_files_with_metadata()
    {
        using var workspace = new TempDirectory();
        var libraryPath = Path.Combine(workspace.Path, "Sounds", "Library");
        Directory.CreateDirectory(libraryPath);
        File.WriteAllBytes(Path.Combine(libraryPath, "success.wav"), new byte[1536]);
        File.WriteAllBytes(Path.Combine(libraryPath, "alert.mp3"), new byte[2 * 1024 * 1024]);
        File.WriteAllText(Path.Combine(libraryPath, "readme.txt"), "ignore me");
        var library = new PortableSoundLibrary(libraryPath);

        var sounds = library.ListSounds();

        Assert.Collection(
            sounds,
            first =>
            {
                Assert.Equal("alert.mp3", first.Name);
                Assert.Equal("MP3", first.Category);
                Assert.Equal("2.00 MB", first.Size);
                Assert.Equal("0:00", first.Duration);
            },
            second =>
            {
                Assert.Equal("success.wav", second.Name);
                Assert.Equal("WAV", second.Category);
                Assert.Equal("1.50 KB", second.Size);
            });
    }

    [Fact]
    public async Task ImportSound_copies_supported_file_and_avoids_duplicate_names()
    {
        using var workspace = new TempDirectory();
        var sourcePath = Path.Combine(workspace.Path, "source", "ding.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "first");
        var library = new PortableSoundLibrary(Path.Combine(workspace.Path, "Sounds", "Library"));

        var firstImport = await library.ImportSoundAsync(sourcePath);
        await File.WriteAllTextAsync(sourcePath, "second");
        var secondImport = await library.ImportSoundAsync(sourcePath);

        Assert.Equal("ding.wav", firstImport.Name);
        Assert.Equal("ding (1).wav", secondImport.Name);
        Assert.True(File.Exists(firstImport.FilePath));
        Assert.True(File.Exists(secondImport.FilePath));
        Assert.Equal("first", await File.ReadAllTextAsync(firstImport.FilePath));
        Assert.Equal("second", await File.ReadAllTextAsync(secondImport.FilePath));
    }

    [Theory]
    [InlineData("note.txt")]
    [InlineData("sound.aiff")]
    public async Task ImportSound_rejects_unsupported_extensions(string fileName)
    {
        using var workspace = new TempDirectory();
        var sourcePath = Path.Combine(workspace.Path, fileName);
        await File.WriteAllTextAsync(sourcePath, "unsupported");
        var library = new PortableSoundLibrary(Path.Combine(workspace.Path, "Sounds", "Library"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => library.ImportSoundAsync(sourcePath));

        Assert.Contains("仅支持 WAV 和 MP3 音效文件", ex.Message);
    }

    [Fact]
    public async Task DeleteSound_removes_only_files_inside_library()
    {
        using var workspace = new TempDirectory();
        var libraryPath = Path.Combine(workspace.Path, "Sounds", "Library");
        Directory.CreateDirectory(libraryPath);
        var insidePath = Path.Combine(libraryPath, "ding.wav");
        var outsidePath = Path.Combine(workspace.Path, "outside.wav");
        await File.WriteAllTextAsync(insidePath, "inside");
        await File.WriteAllTextAsync(outsidePath, "outside");
        var library = new PortableSoundLibrary(libraryPath);

        Assert.True(library.DeleteSound(insidePath));
        var ex = Assert.Throws<InvalidOperationException>(() => library.DeleteSound(outsidePath));

        Assert.False(File.Exists(insidePath));
        Assert.True(File.Exists(outsidePath));
        Assert.Contains("音效文件必须位于音效库目录内", ex.Message);
    }

    [Fact]
    public void CreateOpenFolderPlan_creates_library_directory_and_uses_macos_open()
    {
        using var workspace = new TempDirectory();
        var libraryPath = Path.Combine(workspace.Path, "Sounds", "Library");
        var library = new PortableSoundLibrary(libraryPath);

        var plan = library.CreateOpenFolderPlan();

        Assert.True(Directory.Exists(libraryPath));
        Assert.Equal("/usr/bin/open", plan.FileName);
        Assert.Equal([libraryPath], plan.Arguments);
    }
}

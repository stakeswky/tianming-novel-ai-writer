using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Packaging;
using Xunit;

namespace Tianming.ProjectData.Tests.Packaging;

public class ZipBookExporterTests
{
    [Fact]
    public async Task Export_creates_zip_containing_chapters_and_design()
    {
        using var workspace = new TempDirectory("tm-exp");
        var chaptersDir = Path.Combine(workspace.Path, "Generated", "chapters");
        var designDir = Path.Combine(workspace.Path, "Design", "Elements", "Characters");
        Directory.CreateDirectory(chaptersDir);
        Directory.CreateDirectory(designDir);
        await File.WriteAllTextAsync(Path.Combine(chaptersDir, "ch-001.md"), "Chapter 1");
        await File.WriteAllTextAsync(Path.Combine(designDir, "char-001.json"), "{\"Name\":\"x\"}");

        using var output = new TempFile(".zip");
        var exporter = new ZipBookExporter();

        await exporter.ExportAsync(workspace.Path, output.Path);

        Assert.True(File.Exists(output.Path));
        using var zip = ZipFile.OpenRead(output.Path);
        Assert.Contains(zip.Entries, entry => entry.FullName.EndsWith("ch-001.md"));
        Assert.Contains(zip.Entries, entry => entry.FullName.EndsWith("char-001.json"));
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains(".staged/"));
    }

    [Fact]
    public async Task Export_excludes_blacklisted_directories()
    {
        using var workspace = new TempDirectory("tm-exp");
        var walDir = Path.Combine(workspace.Path, ".wal");
        var stagedDir = Path.Combine(workspace.Path, ".staged");
        var binDir = Path.Combine(workspace.Path, "bin");
        Directory.CreateDirectory(walDir);
        Directory.CreateDirectory(stagedDir);
        Directory.CreateDirectory(binDir);
        await File.WriteAllTextAsync(Path.Combine(walDir, "x.jsonl"), "{}");
        await File.WriteAllTextAsync(Path.Combine(stagedDir, "x.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(binDir, "x.dll"), "x");

        using var output = new TempFile(".zip");
        var exporter = new ZipBookExporter();

        await exporter.ExportAsync(workspace.Path, output.Path);

        using var zip = ZipFile.OpenRead(output.Path);
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains(".wal/"));
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains(".staged/"));
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains("bin/"));
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public TempDirectory(string prefix)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class TempFile : System.IDisposable
    {
        public TempFile(string extension)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-out-{System.Guid.NewGuid():N}{extension}");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}

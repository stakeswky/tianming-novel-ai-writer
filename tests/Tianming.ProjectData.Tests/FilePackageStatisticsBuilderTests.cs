using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FilePackageStatisticsBuilderTests
{
    [Fact]
    public async Task BuildStatisticsAsync_counts_chapters_words_characters_and_locations()
    {
        using var workspace = new TempDirectory();
        var chaptersDir = System.IO.Path.Combine(workspace.Path, "Generated");
        var publishedRoot = System.IO.Path.Combine(workspace.Path, "Published");
        Directory.CreateDirectory(chaptersDir);
        Directory.CreateDirectory(System.IO.Path.Combine(publishedRoot, "Design"));

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(chaptersDir, "vol1_ch001.md"),
            """
            # 第一章
            星火落入山门，少年回望故乡。
            English words are ignored.
            """);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(chaptersDir, "vol1_ch002.md"),
            """
            **第二章**
            剑鸣如潮，旧约重燃。
            """);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(publishedRoot, "Design", "elements.json"),
            """
            {
              "data": {
                "characterrules": {
                  "characters": [
                    { "Id": "C1", "Name": "林衡" },
                    { "Id": "C2", "Name": "陆澜" }
                  ],
                  "relationships": [
                    { "Id": "R1" }
                  ]
                },
                "locationrules": {
                  "locations": [
                    { "Id": "L1", "Name": "山门" },
                    { "Id": "L2", "Name": "旧城" }
                  ]
                }
              }
            }
            """);

        var builder = new FilePackageStatisticsBuilder(chaptersDir, publishedRoot);

        var stats = await builder.BuildStatisticsAsync();

        Assert.Equal(2, stats.TotalChapters);
        Assert.Equal(26, stats.TotalWords);
        Assert.Equal(3, stats.TotalCharacters);
        Assert.Equal(2, stats.TotalLocations);
    }

    [Fact]
    public async Task BuildStatisticsAsync_returns_zeroes_when_inputs_are_missing()
    {
        using var workspace = new TempDirectory();
        var builder = new FilePackageStatisticsBuilder(
            System.IO.Path.Combine(workspace.Path, "MissingChapters"),
            System.IO.Path.Combine(workspace.Path, "MissingPublished"));

        var stats = await builder.BuildStatisticsAsync();

        Assert.Equal(0, stats.TotalChapters);
        Assert.Equal(0, stats.TotalWords);
        Assert.Equal(0, stats.TotalCharacters);
        Assert.Equal(0, stats.TotalLocations);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-package-statistics-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

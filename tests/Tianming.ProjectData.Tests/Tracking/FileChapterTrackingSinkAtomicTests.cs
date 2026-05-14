using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class FileChapterTrackingSinkAtomicTests
{
    [Fact]
    public async Task Save_uses_temp_then_rename()
    {
        using var workspace = new TempDirectory();
        var sink = new FileChapterTrackingSink(workspace.Path);
        var targetPath = Path.Combine(workspace.Path, "character_state_guide_vol1.json");
        var tempPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, "stale");

        await sink.UpdateCharacterStateAsync("vol1_ch1", new CharacterStateChange { CharacterId = "char-001" });

        Assert.True(File.Exists(targetPath));
        Assert.False(File.Exists(tempPath));
        Assert.Empty(Directory.GetFiles(workspace.Path, "*.tmp", SearchOption.AllDirectories));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-tracking-atomic-{Guid.NewGuid():N}");

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

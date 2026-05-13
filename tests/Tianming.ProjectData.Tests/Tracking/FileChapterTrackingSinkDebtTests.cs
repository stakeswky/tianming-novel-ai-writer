using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class FileChapterTrackingSinkDebtTests
{
    [Fact]
    public async Task RecordTrackingDebts_persists_and_loads_back()
    {
        using var workspace = new TempDirectory();
        var sink = new FileChapterTrackingSink(workspace.Path);

        var debts = new List<TrackingDebt>
        {
            new() { Id = "d-1", Category = TrackingDebtCategory.Pledge, ChapterId = "vol1_ch5", Description = "测试" },
            new() { Id = "d-2", Category = TrackingDebtCategory.Deadline, ChapterId = "vol1_ch6", Description = "测试 2" },
        };
        await sink.RecordTrackingDebtsAsync("vol1_ch5", debts);

        var loaded = await sink.LoadTrackingDebtsAsync(volume: 1);

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, debt => debt.Id == "d-1");
        Assert.Contains(loaded, debt => debt.Category == TrackingDebtCategory.Deadline);

        var persisted = await ReadJsonAsync<List<TrackingDebt>>(workspace.Path, "tracking_debts_vol1.json");
        Assert.Equal(2, persisted.Count);
    }

    private static async Task<T> ReadJsonAsync<T>(string root, string relativePath)
    {
        var json = await File.ReadAllTextAsync(System.IO.Path.Combine(root, relativePath));
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-tracking-debts-{Guid.NewGuid():N}");

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

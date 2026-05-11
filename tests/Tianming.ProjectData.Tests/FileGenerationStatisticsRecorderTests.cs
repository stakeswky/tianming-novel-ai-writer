using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileGenerationStatisticsRecorderTests
{
    [Fact]
    public void RecordGeneration_persists_statistics_to_explicit_json_file()
    {
        using var workspace = new TempDirectory();
        var statisticsPath = Path.Combine(workspace.Path, "config", "generation_statistics.json");
        var recorder = new FileGenerationStatisticsRecorder(statisticsPath);

        recorder.RecordGeneration(new GenerationResult
        {
            ChapterId = "vol1_ch1",
            Success = true,
            Attempts = { new GenerationAttempt { AttemptNumber = 1, Success = true } }
        });
        recorder.RecordGeneration(new GenerationResult
        {
            ChapterId = "vol1_ch2",
            Success = false,
            RequiresManualIntervention = true,
            Attempts =
            {
                new GenerationAttempt { AttemptNumber = 1, Success = false, FailureReasons = ["[Protocol] bad json"] }
            }
        });

        var reloaded = new FileGenerationStatisticsRecorder(statisticsPath);
        var statistics = reloaded.GetStatistics();

        Assert.Equal(2, statistics.TotalGenerations);
        Assert.Equal(1, statistics.FirstPassCount);
        Assert.Equal(1, statistics.FinalFailureCount);
        Assert.Equal(1, statistics.ProtocolFailureCount);
    }

    [Fact]
    public void ResetStatistics_persists_empty_statistics()
    {
        using var workspace = new TempDirectory();
        var statisticsPath = Path.Combine(workspace.Path, "generation_statistics.json");
        var recorder = new FileGenerationStatisticsRecorder(statisticsPath);
        recorder.RecordGeneration(new GenerationResult
        {
            ChapterId = "vol1_ch1",
            Success = true,
            Attempts = { new GenerationAttempt { AttemptNumber = 1, Success = true } }
        });

        recorder.ResetStatistics();

        var reloaded = new FileGenerationStatisticsRecorder(statisticsPath);
        Assert.Equal(0, reloaded.GetStatistics().TotalGenerations);
        Assert.Empty(reloaded.GetRecentRecords(10));
    }

    [Fact]
    public void Constructor_ignores_corrupt_json_and_recovers_on_next_save()
    {
        using var workspace = new TempDirectory();
        var statisticsPath = Path.Combine(workspace.Path, "generation_statistics.json");
        File.WriteAllText(statisticsPath, "{ not-json");

        var recorder = new FileGenerationStatisticsRecorder(statisticsPath);
        recorder.RecordConsistencyIssue("ConflictStatusSkip");

        var reloaded = new FileGenerationStatisticsRecorder(statisticsPath);
        Assert.Equal(1, reloaded.GetStatistics().ConsistencyIssues.ConflictStatusSkip);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-generation-stats-{Guid.NewGuid():N}");

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

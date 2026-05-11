using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class GenerationStatisticsRecorderTests
{
    [Fact]
    public void GenerationResult_tracks_attempts_and_last_failure_reasons()
    {
        var result = new GenerationResult { ChapterId = "vol1_ch1" };

        result.AddAttempt(1, false, "协议失败", ["[Protocol] missing"]);
        result.AddAttempt(2, true, "通过");

        Assert.Equal(2, result.TotalAttempts);
        Assert.Equal(1, result.RewriteCount);
        Assert.Empty(result.GetLastFailureReasons());
        Assert.True(result.Attempts[1].IsRewrite);
    }

    [Fact]
    public void RecordGeneration_updates_first_pass_rewrite_and_failure_statistics()
    {
        var recorder = new GenerationStatisticsRecorder();
        recorder.RecordGeneration(new GenerationResult
        {
            ChapterId = "vol1_ch1",
            Success = true,
            Attempts = { new GenerationAttempt { AttemptNumber = 1, Success = true } }
        });
        recorder.RecordGeneration(new GenerationResult
        {
            ChapterId = "vol1_ch2",
            Success = true,
            Attempts =
            {
                new GenerationAttempt { AttemptNumber = 1, Success = false, FailureReasons = ["[Consistency] [ConflictStatusSkip] 实体: K7"] },
                new GenerationAttempt { AttemptNumber = 2, Success = true }
            }
        });
        recorder.RecordGeneration(new GenerationResult
        {
            ChapterId = "vol1_ch3",
            Success = false,
            RequiresManualIntervention = true,
            Attempts =
            {
                new GenerationAttempt { AttemptNumber = 1, Success = false, FailureReasons = ["[Protocol] bad json"] }
            }
        });

        var statistics = recorder.GetStatistics();
        var records = recorder.GetRecentRecords(10);

        Assert.Equal(3, statistics.TotalGenerations);
        Assert.Equal(1, statistics.FirstPassCount);
        Assert.Equal(1, statistics.RewritePassCount);
        Assert.Equal(1, statistics.FinalFailureCount);
        Assert.Equal(1, statistics.ProtocolFailureCount);
        Assert.Equal(1, statistics.ConsistencyFailureCount);
        Assert.Equal(1, statistics.ConsistencyIssues.ConflictStatusSkip);
        Assert.Equal(3, records.Count);
        Assert.Contains("Attempt1:Consistency", records[1].FailureStages);
    }
}

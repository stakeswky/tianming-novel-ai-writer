using System.Text.Json;
using TM.Services.Framework.AI.Monitoring;
using Xunit;

namespace Tianming.AI.Tests;

public class FileUsageStatisticsServiceTests
{
    [Fact]
    public void RecordCall_updates_summary_and_persists_records()
    {
        using var workspace = new TempDirectory();
        var path = System.IO.Path.Combine(workspace.Path, "api_statistics.json");
        var service = new FileUsageStatisticsService(path);

        service.RecordCall("gpt-4.1", "openai", success: true, responseTimeMs: 120, inputTokens: 10, outputTokens: 30);
        service.RecordCall("gpt-4.1", "openai", success: false, responseTimeMs: 240, inputTokens: 5, outputTokens: 0, errorMessage: "timeout");

        var summary = service.GetSummary();
        var reloaded = new FileUsageStatisticsService(path);

        Assert.Equal(2, summary.TotalCalls);
        Assert.Equal(1, summary.SuccessCalls);
        Assert.Equal(1, summary.FailedCalls);
        Assert.Equal(50, summary.SuccessRate);
        Assert.Equal(180, summary.AverageResponseTime);
        Assert.Equal(15, summary.TotalInputTokens);
        Assert.Equal(30, summary.TotalOutputTokens);
        Assert.Equal(2, reloaded.GetAllRecords().Count);
    }

    [Fact]
    public void Query_methods_return_recent_daily_and_model_summaries()
    {
        using var workspace = new TempDirectory();
        var path = System.IO.Path.Combine(workspace.Path, "api_statistics.json");
        var now = DateTime.Now;
        Seed(path,
        [
            Record("A", "openai", true, 100, now.Date.AddDays(-1).AddHours(10), 10, 20),
            Record("B", "openai", false, 300, now.Date.AddHours(9), 5, 0),
            Record("A", "openai", true, 200, now.Date.AddHours(10), 7, 8)
        ]);
        var service = new FileUsageStatisticsService(path);

        var recent = service.GetRecentRecords(2);
        var daily = service.GetDailyStatistics(2);
        var byModel = service.GetStatisticsByModel();

        Assert.Equal(["A", "B"], recent.Select(record => record.ModelName).ToArray());
        Assert.Equal(2, daily.Count);
        Assert.Equal(2, daily.Last().TotalCalls);
        Assert.Equal(2, byModel["A"].TotalCalls);
        Assert.Equal(1, byModel["B"].FailedCalls);
    }

    [Fact]
    public void Constructor_trims_expired_records_and_clear_statistics_persists_empty_file()
    {
        using var workspace = new TempDirectory();
        var path = System.IO.Path.Combine(workspace.Path, "api_statistics.json");
        Seed(path,
        [
            Record("old", "openai", true, 100, DateTime.Now.AddDays(-5), 1, 1),
            Record("fresh", "openai", true, 100, DateTime.Now.AddHours(-1), 1, 1)
        ]);

        var service = new FileUsageStatisticsService(path, retentionDays: 3);
        service.ClearStatistics();
        var reloaded = new FileUsageStatisticsService(path);

        Assert.Empty(service.GetAllRecords());
        Assert.Empty(reloaded.GetAllRecords());
    }

    private static ApiCallRecord Record(
        string model,
        string provider,
        bool success,
        int responseTimeMs,
        DateTime timestamp,
        int inputTokens,
        int outputTokens)
    {
        return new ApiCallRecord
        {
            ModelName = model,
            Provider = provider,
            Success = success,
            ResponseTimeMs = responseTimeMs,
            Timestamp = timestamp,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
    }

    private static void Seed(string path, IReadOnlyList<ApiCallRecord> records)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(records));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-ai-stats-{Guid.NewGuid():N}");

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

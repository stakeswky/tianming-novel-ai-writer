using TM.Framework.Logging;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLogOutputTelemetryStoreTests
{
    [Fact]
    public async Task RecordAttemptAsync_updates_statistics_and_round_trips()
    {
        using var workspace = new TempDirectory();
        var store = CreateStore(workspace);

        await store.RecordAttemptAsync("file", PortableLogOutputTargetType.File, true, 10, 100);
        await store.RecordAttemptAsync("file", PortableLogOutputTargetType.File, false, 30, 0);
        await store.RecordAttemptAsync("console", PortableLogOutputTargetType.Console, true, 0, 5);

        var reloaded = CreateStore(workspace);
        var stats = await reloaded.LoadStatisticsAsync();

        var file = Assert.Single(stats, stat => stat.TargetName == "file");
        Assert.Equal(2, file.TotalAttempts);
        Assert.Equal(1, file.SuccessCount);
        Assert.Equal(1, file.FailureCount);
        Assert.Equal(20, file.AverageResponseTimeMs);
        Assert.Equal(100, file.TotalBytes);
        Assert.Equal(50, file.SuccessRate);

        var console = Assert.Single(stats, stat => stat.TargetName == "console");
        Assert.Equal(1, console.TotalAttempts);
        Assert.Equal(100, console.SuccessRate);
    }

    [Fact]
    public async Task RecordFailureAsync_keeps_newest_first_and_trims_to_100()
    {
        using var workspace = new TempDirectory();
        var store = CreateStore(workspace, () => new DateTime(2026, 5, 10, 9, 0, 0));

        for (var i = 0; i < 105; i++)
        {
            await store.RecordFailureAsync(
                "remote",
                PortableLogOutputTargetType.RemoteHttp,
                $"error-{i}",
                $"line-{i}");
        }

        var failures = await CreateStore(workspace).LoadFailuresAsync();

        Assert.Equal(100, failures.Count);
        Assert.Equal("error-104", failures[0].ErrorMessage);
        Assert.Equal("line-104", failures[0].LogContent);
        Assert.Equal("error-5", failures[^1].ErrorMessage);
    }

    [Fact]
    public async Task ApplyDispatchResultAsync_records_attempts_and_failures()
    {
        using var workspace = new TempDirectory();
        var store = CreateStore(workspace);
        var result = new PortableLogOutputDispatchResult();
        result.Attempts.Add(new PortableLogOutputAttempt("file", PortableLogOutputTargetType.File, true));
        result.Attempts.Add(new PortableLogOutputAttempt("remote", PortableLogOutputTargetType.RemoteHttp, false, "timeout"));
        result.Failures.Add(new PortableLogOutputFailureRecord(
            new DateTime(2026, 5, 10, 9, 0, 0),
            "remote",
            PortableLogOutputTargetType.RemoteHttp,
            "timeout",
            "payload"));

        await store.ApplyDispatchResultAsync(result, responseTimesMs: new Dictionary<string, long>
        {
            ["file"] = 12,
            ["remote"] = 34
        });

        var stats = await store.LoadStatisticsAsync();
        Assert.Equal(2, stats.Count);
        Assert.Equal(34, stats.Single(stat => stat.TargetName == "remote").AverageResponseTimeMs);
        var failure = Assert.Single(await store.LoadFailuresAsync());
        Assert.Equal("timeout", failure.ErrorMessage);
        Assert.Equal("payload", failure.LogContent);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_missing_or_bad_json_and_reset_clears_files()
    {
        using var workspace = new TempDirectory();
        var store = CreateStore(workspace);
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "statistics.json"), "{ bad");
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "failures.json"), "{ bad");

        Assert.Empty(await store.LoadStatisticsAsync());
        Assert.Empty(await store.LoadFailuresAsync());

        await store.RecordAttemptAsync("file", PortableLogOutputTargetType.File, true, 1, 2);
        await store.RecordFailureAsync("file", PortableLogOutputTargetType.File, "error", "line");
        await store.ResetStatisticsAsync();
        await store.ClearFailuresAsync();

        Assert.Empty(await store.LoadStatisticsAsync());
        Assert.Empty(await store.LoadFailuresAsync());
    }

    private static PortableLogOutputTelemetryStore CreateStore(
        TempDirectory workspace,
        Func<DateTime>? clock = null)
    {
        return new PortableLogOutputTelemetryStore(
            Path.Combine(workspace.Path, "statistics.json"),
            Path.Combine(workspace.Path, "failures.json"),
            clock ?? (() => new DateTime(2026, 5, 10, 8, 0, 0)));
    }
}

using TM.Framework.Logging;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLogOutputTestRunnerTests
{
    [Fact]
    public async Task TestAllAsync_dispatches_enabled_targets_and_records_telemetry()
    {
        using var workspace = new TempDirectory();
        var settings = PortableLogOutputSettings.CreateDefault();
        settings.EnableFileOutput = false;
        settings.EnableConsoleOutput = true;
        settings.EnableEventLog = true;
        settings.EventLogSource = "TM";
        settings.EnableRemoteOutput = false;
        var consoleSink = new RecordingLogOutputSink(PortableLogOutputTargetType.Console);
        var eventSink = new RecordingLogOutputSink(
            PortableLogOutputTargetType.EventLog,
            _ => throw new InvalidOperationException("logger unavailable"));
        var telemetry = CreateTelemetry(workspace);
        var runner = new PortableLogOutputTestRunner(
            new PortableLogOutputDispatcher([consoleSink, eventSink]),
            telemetry,
            () => new DateTime(2026, 5, 10, 12, 0, 0));

        var results = await runner.TestAllAsync(settings, "probe-line");

        Assert.Equal(["console:probe-line"], consoleSink.Writes);
        Assert.Equal(["console", "event-log"], results.Select(result => result.TargetName));
        Assert.Equal(PortableLogOutputTestStatus.Success, results[0].Status);
        Assert.Equal(PortableLogOutputTestStatus.Failed, results[1].Status);
        Assert.Contains("logger unavailable", results[1].Message);

        var stats = await telemetry.LoadStatisticsAsync();
        Assert.Equal(2, stats.Count);
        Assert.Equal(1, stats.Single(stat => stat.TargetName == "console").SuccessCount);
        Assert.Equal(1, stats.Single(stat => stat.TargetName == "event-log").FailureCount);
        var failure = Assert.Single(await telemetry.LoadFailuresAsync());
        Assert.Equal("event-log", failure.TargetName);
        Assert.Equal("probe-line", failure.LogContent);
    }

    [Fact]
    public async Task TestAllAsync_returns_empty_when_no_targets_are_enabled()
    {
        using var workspace = new TempDirectory();
        var settings = PortableLogOutputSettings.CreateDefault();
        settings.EnableFileOutput = false;
        settings.EnableConsoleOutput = false;
        settings.EnableEventLog = false;
        settings.EnableRemoteOutput = false;
        var runner = new PortableLogOutputTestRunner(
            new PortableLogOutputDispatcher([]),
            CreateTelemetry(workspace));

        var results = await runner.TestAllAsync(settings, "probe-line");

        Assert.Empty(results);
        Assert.Empty(await CreateTelemetry(workspace).LoadStatisticsAsync());
    }

    private static PortableLogOutputTelemetryStore CreateTelemetry(TempDirectory workspace)
    {
        return new PortableLogOutputTelemetryStore(
            Path.Combine(workspace.Path, "statistics.json"),
            Path.Combine(workspace.Path, "failures.json"),
            () => new DateTime(2026, 5, 10, 12, 0, 0));
    }

    private sealed class RecordingLogOutputSink : IPortableLogOutputSink
    {
        private readonly Func<string, Task>? _writeAsync;

        public RecordingLogOutputSink(
            PortableLogOutputTargetType targetType,
            Func<string, Task>? writeAsync = null)
        {
            TargetType = targetType;
            _writeAsync = writeAsync;
        }

        public PortableLogOutputTargetType TargetType { get; }

        public List<string> Writes { get; } = new();

        public async Task WriteAsync(
            PortableLogOutputTarget target,
            string content,
            CancellationToken cancellationToken = default)
        {
            if (_writeAsync is not null)
            {
                await _writeAsync(content);
                return;
            }

            Writes.Add($"{target.Name}:{content}");
        }
    }
}

using TM.Framework.Logging;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLogOutputSettingsControllerTests
{
    [Fact]
    public async Task Controller_loads_adds_removes_and_saves_targets()
    {
        using var workspace = new TempDirectory();
        var settingsPath = Path.Combine(workspace.Path, "settings.json");
        var store = new FileLogOutputSettingsStore(settingsPath);
        var initial = PortableLogOutputSettings.CreateDefault();
        initial.EnableConsoleOutput = false;
        initial.OutputTargets.Add(new PortableLogOutputTarget
        {
            Name = "existing",
            Type = PortableLogOutputTargetType.File,
            Priority = 0
        });
        await store.SaveAsync(initial);
        var controller = CreateController(workspace, store);

        var loaded = await controller.LoadAsync();
        var added = controller.AddTarget(" remote json ", PortableLogOutputTargetType.RemoteHttp, new Dictionary<string, string>
        {
            ["Address"] = "https://logs.example.test/ingest"
        });
        var removed = controller.RemoveTarget("existing");
        await controller.SaveAsync();

        Assert.False(loaded.EnableConsoleOutput);
        Assert.NotNull(added);
        Assert.Equal("remote json", added.Name);
        Assert.Equal(1, added.Priority);
        Assert.True(removed);
        var reloaded = await store.LoadAsync();
        var target = Assert.Single(reloaded.OutputTargets);
        Assert.Equal("remote json", target.Name);
        Assert.Equal(PortableLogOutputTargetType.RemoteHttp, target.Type);
        Assert.Equal("https://logs.example.test/ingest", target.Settings["Address"]);
    }

    [Fact]
    public async Task Controller_tests_outputs_and_clears_telemetry()
    {
        using var workspace = new TempDirectory();
        var telemetry = CreateTelemetry(workspace);
        var controller = CreateController(
            workspace,
            new FileLogOutputSettingsStore(Path.Combine(workspace.Path, "settings.json")),
            telemetry,
            new PortableLogOutputTestRunner(
                new PortableLogOutputDispatcher([new RecordingLogOutputSink(PortableLogOutputTargetType.Console)]),
                telemetry,
                () => new DateTime(2026, 5, 10, 13, 0, 0)));
        await controller.LoadAsync();
        controller.Settings.EnableFileOutput = false;
        controller.Settings.EnableConsoleOutput = true;
        controller.Settings.EnableEventLog = false;
        controller.Settings.EnableRemoteOutput = false;

        var results = await controller.TestAllOutputsAsync("controller-probe");
        await controller.ClearTelemetryAsync();

        var result = Assert.Single(results);
        Assert.Equal("console", result.TargetName);
        Assert.Equal(PortableLogOutputTestStatus.Success, result.Status);
        Assert.Empty(await telemetry.LoadStatisticsAsync());
        Assert.Empty(await telemetry.LoadFailuresAsync());
    }

    private static PortableLogOutputSettingsController CreateController(
        TempDirectory workspace,
        FileLogOutputSettingsStore store,
        PortableLogOutputTelemetryStore? telemetry = null,
        PortableLogOutputTestRunner? runner = null)
    {
        telemetry ??= CreateTelemetry(workspace);
        runner ??= new PortableLogOutputTestRunner(new PortableLogOutputDispatcher([]), telemetry);
        return new PortableLogOutputSettingsController(store, runner, telemetry);
    }

    private static PortableLogOutputTelemetryStore CreateTelemetry(TempDirectory workspace)
    {
        return new PortableLogOutputTelemetryStore(
            Path.Combine(workspace.Path, "statistics.json"),
            Path.Combine(workspace.Path, "failures.json"),
            () => new DateTime(2026, 5, 10, 13, 0, 0));
    }

    private sealed class RecordingLogOutputSink : IPortableLogOutputSink
    {
        public RecordingLogOutputSink(PortableLogOutputTargetType targetType)
        {
            TargetType = targetType;
        }

        public PortableLogOutputTargetType TargetType { get; }

        public Task WriteAsync(
            PortableLogOutputTarget target,
            string content,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

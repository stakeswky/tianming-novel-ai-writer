using TM.Framework.Logging;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLogOutputSettingsStoreTests
{
    [Fact]
    public void Default_settings_match_original_log_output_defaults()
    {
        var settings = PortableLogOutputSettings.CreateDefault();

        Assert.True(settings.EnableFileOutput);
        Assert.Equal("Logs/application.log", settings.FileOutputPath);
        Assert.Equal("{date}_{appname}.log", settings.FileNamingPattern);
        Assert.Equal(PortableLogFileEncodingType.UTF8, settings.FileEncoding);
        Assert.True(settings.EnableConsoleOutput);
        Assert.True(settings.ConsoleColorCoding);
        Assert.False(settings.EnableEventLog);
        Assert.Equal("TM", settings.EventLogSource);
        Assert.False(settings.EnableRemoteOutput);
        Assert.Equal("HTTP", settings.RemoteProtocol);
        Assert.Equal("http://localhost:8080/log", settings.RemoteAddress);
        Assert.Equal(1024, settings.BufferSize);
        Assert.True(settings.EnableAsyncOutput);
        Assert.Empty(settings.OutputTargets);
        Assert.True(settings.RetryConfiguration.EnableRetry);
        Assert.Equal(3, settings.RetryConfiguration.MaxRetryAttempts);
        Assert.Equal(1000, settings.RetryConfiguration.RetryIntervalMs);
        Assert.True(settings.RetryConfiguration.EnableExponentialBackoff);
        Assert.Equal(10000, settings.RetryConfiguration.MaxRetryIntervalMs);
    }

    [Fact]
    public async Task Store_round_trips_settings_and_explicit_targets_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "SystemSettings", "Logging", "LogOutput", "settings.json");
        var store = new FileLogOutputSettingsStore(path);
        var settings = PortableLogOutputSettings.CreateDefault();
        settings.EnableConsoleOutput = false;
        settings.EnableEventLog = true;
        settings.EventLogSource = "TianmingWriter";
        settings.EnableRemoteOutput = true;
        settings.RemoteProtocol = "TCP";
        settings.RemoteAddress = "127.0.0.1:1514";
        settings.OutputTargets.Add(new PortableLogOutputTarget
        {
            Name = "custom-http",
            Type = PortableLogOutputTargetType.RemoteHttp,
            IsEnabled = true,
            Priority = 40,
            Settings =
            {
                ["Address"] = "https://logs.example.test/ingest",
                ["ContentType"] = "application/json"
            }
        });

        await store.SaveAsync(settings);
        var reloaded = await new FileLogOutputSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.False(reloaded.EnableConsoleOutput);
        Assert.True(reloaded.EnableEventLog);
        Assert.Equal("TianmingWriter", reloaded.EventLogSource);
        Assert.True(reloaded.EnableRemoteOutput);
        Assert.Equal("TCP", reloaded.RemoteProtocol);
        Assert.Equal("127.0.0.1:1514", reloaded.RemoteAddress);
        var target = Assert.Single(reloaded.OutputTargets);
        Assert.Equal("custom-http", target.Name);
        Assert.Equal(PortableLogOutputTargetType.RemoteHttp, target.Type);
        Assert.Equal("https://logs.example.test/ingest", target.Settings["Address"]);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var missingStore = new FileLogOutputSettingsStore(path);

        Assert.Equal("TM", (await missingStore.LoadAsync()).EventLogSource);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var invalidStore = new FileLogOutputSettingsStore(path);

        Assert.True((await invalidStore.LoadAsync()).EnableConsoleOutput);
    }

    [Fact]
    public void TargetBuilder_maps_enabled_settings_to_pipeline_targets()
    {
        var settings = PortableLogOutputSettings.CreateDefault();
        settings.FileOutputPath = "Logs/current.log";
        settings.EnableEventLog = true;
        settings.EventLogSource = "TM";
        settings.EnableRemoteOutput = true;
        settings.RemoteProtocol = "TCP";
        settings.RemoteAddress = "127.0.0.1:1514";
        settings.OutputTargets.Add(new PortableLogOutputTarget
        {
            Name = "extra-http",
            Type = PortableLogOutputTargetType.RemoteHttp,
            IsEnabled = true,
            Priority = 40,
            Settings = { ["Address"] = "https://logs.example.test/ingest" }
        });

        var targets = PortableLogOutputTargetBuilder.BuildTargets(settings).ToList();

        Assert.Equal(["file", "console", "event-log", "remote", "extra-http"], targets.Select(target => target.Name));
        Assert.Equal(PortableLogOutputTargetType.File, targets[0].Type);
        Assert.Equal("Logs/current.log", targets[0].Settings["Path"]);
        Assert.Equal(PortableLogOutputTargetType.Console, targets[1].Type);
        Assert.Equal(PortableLogOutputTargetType.EventLog, targets[2].Type);
        Assert.Equal("TM", targets[2].Settings["Tag"]);
        Assert.Equal(PortableLogOutputTargetType.RemoteTcp, targets[3].Type);
        Assert.Equal("127.0.0.1:1514", targets[3].Settings["Address"]);
        Assert.Equal(PortableLogOutputTargetType.RemoteHttp, targets[4].Type);
    }
}
